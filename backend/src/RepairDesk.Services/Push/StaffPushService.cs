using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Push;

public interface IStaffPushService
{
    Task<PushSubscriptionResultDto> SubscribeAsync(BrowserPushSubscriptionDto request, CancellationToken ct = default);
    Task<PushSubscriptionResultDto> UnsubscribeAsync(UnsubscribePushRequest request, CancellationToken ct = default);
    /// <summary>Envia para TODOS os dispositivos de staff do tenant. Chamado pelo worker.</summary>
    Task<int> NotifyTenantAsync(StaffPushJob job, CancellationToken ct = default);
}

public class StaffPushService : IStaffPushService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IStaffPushSubscriptionRepository _subscriptions;
    private readonly IVapidKeyProvider _vapid;
    private readonly IWebPushSender _sender;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IOptions<PushOptions> _options;
    private readonly ILogger<StaffPushService> _logger;

    public StaffPushService(
        IStaffPushSubscriptionRepository subscriptions,
        IVapidKeyProvider vapid,
        IWebPushSender sender,
        ITenantContext tenant,
        ICurrentUser user,
        IOptions<PushOptions> options,
        ILogger<StaffPushService> logger)
    {
        _subscriptions = subscriptions;
        _vapid = vapid;
        _sender = sender;
        _tenant = tenant;
        _user = user;
        _options = options;
        _logger = logger;
    }

    public async Task<PushSubscriptionResultDto> SubscribeAsync(BrowserPushSubscriptionDto request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.Keys?.P256dh) || string.IsNullOrWhiteSpace(request.Keys?.Auth))
        {
            throw new ValidationException("push_keys_invalidas", "Chaves de notificação inválidas.");
        }

        if (_user.UserId is not { } userId || _tenant.TenantId is not { } tenantId)
            throw new ValidationException("push_sem_utilizador", "Sessão sem utilizador para subscrever notificações.");

        var endpoint = request.Endpoint.Trim();
        var existing = await _subscriptions.FindByEndpointAsync(userId, endpoint, ct);
        if (existing is null)
        {
            await _subscriptions.AddAsync(new StaffPushSubscription
            {
                TenantId = tenantId,
                UserId = userId,
                Endpoint = endpoint,
                P256dh = request.Keys.P256dh.Trim(),
                Auth = request.Keys.Auth.Trim(),
            }, ct);
        }
        else
        {
            existing.P256dh = request.Keys.P256dh.Trim();
            existing.Auth = request.Keys.Auth.Trim();
            existing.LastError = null;
            existing.LastErrorAt = null;
        }

        await _subscriptions.SaveAsync(ct);
        return new PushSubscriptionResultDto(true);
    }

    public async Task<PushSubscriptionResultDto> UnsubscribeAsync(UnsubscribePushRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Endpoint) || _user.UserId is not { } userId)
            return new PushSubscriptionResultDto(false);

        var existing = await _subscriptions.FindByEndpointAsync(userId, request.Endpoint.Trim(), ct);
        if (existing is null)
            return new PushSubscriptionResultDto(false);

        _subscriptions.Remove(existing);
        await _subscriptions.SaveAsync(ct);
        return new PushSubscriptionResultDto(false);
    }

    public async Task<int> NotifyTenantAsync(StaffPushJob job, CancellationToken ct = default)
    {
        if (!_options.Value.Enabled)
            return 0;

        var subscriptions = await _subscriptions.ListByTenantAsync(job.TenantId, ct);
        if (subscriptions.Count == 0)
            return 0;

        var keys = await _vapid.GetKeysAsync(ct);
        var payloadJson = JsonSerializer.Serialize(
            new StaffPushPayload(job.Title, job.Body, job.Url, job.Tag), JsonOptions);
        var sent = 0;
        var stale = new List<StaffPushSubscription>();

        foreach (var sub in subscriptions)
        {
            try
            {
                await _sender.SendAsync(new WebPushTarget(sub.Endpoint, sub.P256dh, sub.Auth), payloadJson, keys, ct);
                sub.LastSentAt = DateTime.UtcNow;
                sub.LastError = null;
                sub.LastErrorAt = null;
                sent++;
            }
            catch (Exception ex)
            {
                sub.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                sub.LastErrorAt = DateTime.UtcNow;
                // 404/410 = subscrição morta (browser desinstalado/permissão revogada) → limpar.
                if (ex.Message.Contains("404") || ex.Message.Contains("410") || ex.Message.Contains("Gone", StringComparison.OrdinalIgnoreCase))
                    stale.Add(sub);
                _logger.LogWarning(ex, "Falha a enviar push de staff para endpoint {Endpoint}", sub.Endpoint);
            }
        }

        foreach (var s in stale)
            _subscriptions.Remove(s);

        await _subscriptions.SaveAsync(ct);
        return sent;
    }
}
