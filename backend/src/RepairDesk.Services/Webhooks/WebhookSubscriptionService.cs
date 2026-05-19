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

public class WebhookSubscriptionService : IWebhookSubscriptionService
{
    private readonly IWebhookSubscriptionRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public WebhookSubscriptionService(IWebhookSubscriptionRepository repo, ITenantContext tenant, IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

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
