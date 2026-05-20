using System.Security.Cryptography;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Webhooks;

public interface IWebhookSubscriptionService
{
    Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(CancellationToken ct = default);
    Task<CreateWebhookSubscriptionResponse> CreateAsync(CreateWebhookSubscriptionRequest req, CancellationToken ct = default);
    Task<WebhookSubscriptionDto> UpdateAsync(Guid id, UpdateWebhookSubscriptionRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    IReadOnlyList<string> ListEventTypes();
    Task<IReadOnlyList<WebhookDeliveryDto>> ListDeliveriesAsync(Guid subscriptionId, int take, CancellationToken ct = default);
    /// <summary>Reagenda uma delivery Failed para retry imediato — reset Attempts e Status.</summary>
    Task<WebhookDeliveryDto> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default);
    /// <summary>Stats agregadas para o Dashboard widget (janela de N horas).</summary>
    Task<WebhookStatsDto> GetStatsAsync(int hoursWindow, CancellationToken ct = default);
}

public sealed record WebhookSubscriptionDto(
    Guid Id,
    string Name,
    string Url,
    IReadOnlyList<string> Events,
    bool Active,
    DateTime? LastDeliveryAt,
    int FailureCount,
    DateTime? DisabledAt,
    DateTime CreatedAt);

public sealed record CreateWebhookSubscriptionRequest(
    string Name,
    string Url,
    IReadOnlyList<string> Events);

public sealed record UpdateWebhookSubscriptionRequest(
    string Name,
    string Url,
    IReadOnlyList<string> Events,
    bool Active);

/// <summary>Plain secret devolvido APENAS na criação — depois fica só no servidor (consulta na BD requer SuperAdmin).</summary>
public sealed record CreateWebhookSubscriptionResponse(WebhookSubscriptionDto Subscription, string Secret);

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid WebhookSubscriptionId,
    string EventType,
    string Status,
    int Attempts,
    int? LastResponseCode,
    string? LastError,
    DateTime? NextRetryAt,
    DateTime? DeliveredAt,
    DateTime? FailedAt,
    DateTime CreatedAt,
    string PayloadJson);

public sealed record WebhookStatsDto(
    int ActiveSubscriptions,
    int DisabledSubscriptions,
    int DeliveriesInWindow,
    int DeliveredInWindow,
    int FailedInWindow,
    int PendingNow,
    /// <summary>0-100. -1 quando não há entregas na janela (UI mostra "—").</summary>
    int SuccessRatePercent,
    DateTime? LastDeliveryAt,
    int HoursWindow);

public class WebhookSubscriptionService : IWebhookSubscriptionService
{
    private readonly IWebhookSubscriptionRepository _repo;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public WebhookSubscriptionService(
        IWebhookSubscriptionRepository repo,
        IWebhookDeliveryRepository deliveries,
        ITenantContext tenant,
        IAuditLogger audit)
    {
        _repo = repo;
        _deliveries = deliveries;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<WebhookDeliveryDto>> ListDeliveriesAsync(Guid subscriptionId, int take, CancellationToken ct = default)
    {
        var sub = await _repo.FindByIdAsync(subscriptionId, ct)
            ?? throw new NotFoundException("WebhookSubscription", subscriptionId);
        var rows = await _deliveries.ListBySubscriptionAsync(sub.Id, Math.Clamp(take, 1, 200), ct);
        return rows.Select(ToDeliveryDto).ToList();
    }

    public async Task<WebhookDeliveryDto> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var d = await _deliveries.FindByIdAsync(deliveryId, ct)
            ?? throw new NotFoundException("WebhookDelivery", deliveryId);
        // Validar que a delivery pertence ao tenant actual (defesa em profundidade —
        // FindByIdAsync já aplica tenant filter, mas a verificação extra documenta a intenção).
        if (_tenant.TenantId is { } tid && d.TenantId != tid)
            throw new NotFoundException("WebhookDelivery", deliveryId);

        d.Status = WebhookDeliveryStatus.Pending;
        d.Attempts = 0;
        d.NextRetryAt = DateTime.UtcNow;
        d.FailedAt = null;
        d.LastError = null;
        d.LastResponseCode = null;
        await _deliveries.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, "WebhookDelivery", d.Id, new { action = "manual_retry" }, ct: ct);
        return ToDeliveryDto(d);
    }

    public async Task<WebhookStatsDto> GetStatsAsync(int hoursWindow, CancellationToken ct = default)
    {
        var hours = Math.Clamp(hoursWindow, 1, 24 * 30);
        var since = DateTime.UtcNow.AddHours(-hours);
        var s = await _deliveries.GetStatsAsync(since, ct);
        var rate = s.DeliveriesSinceWindow == 0
            ? -1
            : (int)Math.Round(100.0 * s.DeliveredSinceWindow / s.DeliveriesSinceWindow);
        return new WebhookStatsDto(
            s.ActiveSubscriptions, s.DisabledSubscriptions,
            s.DeliveriesSinceWindow, s.DeliveredSinceWindow, s.FailedSinceWindow, s.PendingNow,
            rate, s.LastDeliveryAt, hours);
    }

    private static WebhookDeliveryDto ToDeliveryDto(WebhookDelivery d) =>
        new(d.Id, d.WebhookSubscriptionId, d.EventType, d.Status.ToString(),
            d.Attempts, d.LastResponseCode, d.LastError,
            d.NextRetryAt, d.DeliveredAt, d.FailedAt, d.CreatedAt, d.PayloadJson);

    public IReadOnlyList<string> ListEventTypes() => WebhookEvents.All;

    public async Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _repo.ListByTenantAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<CreateWebhookSubscriptionResponse> CreateAsync(CreateWebhookSubscriptionRequest req, CancellationToken ct = default)
    {
        var (name, url, events) = ValidateInput(req.Name, req.Url, req.Events);
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var secret = GenerateSecret();
        var sub = new WebhookSubscription
        {
            TenantId = tenantId,
            Name = name,
            Url = url,
            Secret = secret,
            Events = string.Join(',', events),
            Active = true,
        };

        await _repo.AddAsync(sub, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(WebhookSubscription), sub.Id, new { sub.Name, sub.Url, sub.Events }, ct: ct);
        return new CreateWebhookSubscriptionResponse(ToDto(sub), secret);
    }

    public async Task<WebhookSubscriptionDto> UpdateAsync(Guid id, UpdateWebhookSubscriptionRequest req, CancellationToken ct = default)
    {
        var sub = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("WebhookSubscription", id);

        var (name, url, events) = ValidateInput(req.Name, req.Url, req.Events);
        sub.Name = name;
        sub.Url = url;
        sub.Events = string.Join(',', events);
        sub.Active = req.Active;
        if (req.Active)
        {
            sub.DisabledAt = null;
            sub.FailureCount = 0;
        }

        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(WebhookSubscription), sub.Id, new { sub.Name, sub.Url, sub.Events, sub.Active }, ct: ct);
        return ToDto(sub);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("WebhookSubscription", id);

        _repo.Remove(sub);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(WebhookSubscription), id, new { sub.Name, sub.Url }, ct: ct);
    }

    private static (string Name, string Url, IReadOnlyList<string> Events) ValidateInput(string name, string url, IReadOnlyList<string> events)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name_required", "Nome obrigatório.");
        name = name.Trim();
        if (name.Length > 200)
            throw new ValidationException("name_too_long", "Nome até 200 caracteres.");

        if (string.IsNullOrWhiteSpace(url))
            throw new ValidationException("url_required", "URL obrigatório.");
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ValidationException("url_invalid", "URL inválido — tem de ser http(s)://...");
        if (url.Length > 500)
            throw new ValidationException("url_too_long", "URL até 500 caracteres.");

        if (events is null || events.Count == 0)
            throw new ValidationException("events_required", "Subscreve pelo menos um evento.");
        var validated = events.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unknown = validated.Except(WebhookEvents.All, StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0)
            throw new ValidationException("event_unknown", $"Evento desconhecido: {string.Join(", ", unknown)}.");

        return (name, url, validated);
    }

    /// <summary>Gera secret 32 bytes em base64 (~43 chars) — suficiente para HMAC-SHA256.</summary>
    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "whsec_" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static WebhookSubscriptionDto ToDto(WebhookSubscription s) =>
        new(s.Id, s.Name, s.Url,
            s.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            s.Active, s.LastDeliveryAt, s.FailureCount, s.DisabledAt, s.CreatedAt);
}
