using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using WebPush;

namespace RepairDesk.Services.Push;

public interface IPushNotificationService
{
    Task<VapidPublicKeyDto> GetVapidPublicKeyAsync(CancellationToken ct = default);
    Task<PushSubscriptionResultDto> SubscribeAsync(string slug, BrowserPushSubscriptionDto request, CancellationToken ct = default);
    Task<PushSubscriptionResultDto> UnsubscribeAsync(string slug, UnsubscribePushRequest request, CancellationToken ct = default);
    Task<int> SendRepairStatusChangedAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<int> PurgeDeliveredOlderThanAsync(CancellationToken ct = default);
}

public class PushNotificationService : IPushNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReparacaoRepository _reparacoes;
    private readonly IPushSubscriptionRepository _subscriptions;
    private readonly IVapidKeyProvider _vapid;
    private readonly IWebPushSender _sender;
    private readonly IOptions<PushOptions> _options;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        IReparacaoRepository reparacoes,
        IPushSubscriptionRepository subscriptions,
        IVapidKeyProvider vapid,
        IWebPushSender sender,
        IOptions<PushOptions> options,
        ILogger<PushNotificationService> logger)
    {
        _reparacoes = reparacoes;
        _subscriptions = subscriptions;
        _vapid = vapid;
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    public async Task<VapidPublicKeyDto> GetVapidPublicKeyAsync(CancellationToken ct = default)
    {
        var keys = await _vapid.GetKeysAsync(ct);
        return new VapidPublicKeyDto(keys.PublicKey);
    }

    public async Task<PushSubscriptionResultDto> SubscribeAsync(string slug, BrowserPushSubscriptionDto request, CancellationToken ct = default)
    {
        ValidateSubscription(request);
        var rep = await FindRepairBySlugAsync(slug, ct);

        if (rep.Estado == RepairStatus.Entregue && rep.EntregueEm is { } entregueEm
            && entregueEm < DateTime.UtcNow.AddDays(-_options.Value.DeliveredRetentionDays))
        {
            throw new ConflictException("push_reparacao_expirada", "Esta reparação já foi entregue há demasiado tempo para receber notificações.");
        }

        var existing = await _subscriptions.FindByEndpointAsync(rep.Id, request.Endpoint, ct);
        if (existing is null)
        {
            await _subscriptions.AddAsync(new RepairDesk.Core.Entities.PushSubscription
            {
                TenantId = rep.TenantId,
                ReparacaoId = rep.Id,
                Endpoint = request.Endpoint.Trim(),
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

    public async Task<PushSubscriptionResultDto> UnsubscribeAsync(string slug, UnsubscribePushRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
            return new PushSubscriptionResultDto(false);

        var rep = await FindRepairBySlugAsync(slug, ct);
        var existing = await _subscriptions.FindByEndpointAsync(rep.Id, request.Endpoint.Trim(), ct);
        if (existing is null)
            return new PushSubscriptionResultDto(false);

        _subscriptions.Remove(existing);
        await _subscriptions.SaveAsync(ct);
        return new PushSubscriptionResultDto(false);
    }

    public async Task<int> SendRepairStatusChangedAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        if (!_options.Value.Enabled)
            return 0;

        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct);
        if (rep is null || string.IsNullOrWhiteSpace(rep.PublicSlug))
            return 0;

        var subscriptions = await _subscriptions.ListByReparacaoIdAsync(reparacaoId, ct);
        if (subscriptions.Count == 0)
            return 0;

        var keys = await _vapid.GetKeysAsync(ct);
        var payload = BuildPayload(rep);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var sent = 0;

        foreach (var subscription in subscriptions)
        {
            try
            {
                await _sender.SendAsync(
                    new WebPushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                    payloadJson,
                    keys,
                    ct);
                subscription.LastSentAt = DateTime.UtcNow;
                subscription.LastError = null;
                subscription.LastErrorAt = null;
                sent++;
            }
            catch (Exception ex) when (IsExpiredSubscription(ex))
            {
                _subscriptions.Remove(subscription);
                _logger.LogInformation(ex, "Removed expired push subscription for repair {RepairId}", reparacaoId);
            }
            catch (Exception ex)
            {
                subscription.LastErrorAt = DateTime.UtcNow;
                subscription.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                _logger.LogWarning(ex, "Failed to send push notification for repair {RepairId}", reparacaoId);
            }
        }

        await _subscriptions.SaveAsync(ct);
        return sent;
    }

    public async Task<int> PurgeDeliveredOlderThanAsync(CancellationToken ct = default)
    {
        var deliveredBefore = DateTime.UtcNow.AddDays(-_options.Value.DeliveredRetentionDays);
        var old = await _subscriptions.ListDeliveredOlderThanAsync(deliveredBefore, ct);
        if (old.Count == 0)
            return 0;

        _subscriptions.RemoveRange(old);
        await _subscriptions.SaveAsync(ct);
        return old.Count;
    }

    private async Task<Reparacao> FindRepairBySlugAsync(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug);

        return await _reparacoes.FindByPublicSlugWithTimelineAsync(slug.Trim(), ct)
            ?? throw new NotFoundException("Reparacao", slug);
    }

    private static void ValidateSubscription(BrowserPushSubscriptionDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || request.Endpoint.Length > 2048
            || !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ValidationException("push_endpoint_invalido", "Endpoint de notificações inválido.");
        }

        if (request.Keys is null
            || string.IsNullOrWhiteSpace(request.Keys.P256dh)
            || string.IsNullOrWhiteSpace(request.Keys.Auth)
            || request.Keys.P256dh.Length > 512
            || request.Keys.Auth.Length > 256)
        {
            throw new ValidationException("push_keys_invalidas", "Chaves de notificação inválidas.");
        }
    }

    private static PushNotificationPayload BuildPayload(Reparacao rep)
    {
        var title = rep.Estado switch
        {
            RepairStatus.Pronto => $"A tua reparação {rep.Equipamento} está pronta",
            RepairStatus.Entregue => $"Reparação {rep.Equipamento} entregue",
            RepairStatus.Cancelado => $"Atualização da reparação {rep.Equipamento}",
            _ => $"Atualização da reparação {rep.Equipamento}",
        };

        var body = rep.Estado switch
        {
            RepairStatus.Orcamento => "A loja deixou uma atualização no orçamento.",
            RepairStatus.Recebido => "O equipamento já deu entrada na loja.",
            RepairStatus.Diagnostico => "O técnico está a analisar o equipamento.",
            RepairStatus.AguardaPeca => "A reparação está à espera de peça.",
            RepairStatus.EmReparacao => "A reparação está em curso.",
            RepairStatus.Pronto => "Podes passar na loja para levantar quando te der jeito.",
            RepairStatus.Entregue => "Obrigado pela confiança.",
            RepairStatus.Cancelado => "Consulta o portal para veres o estado atualizado.",
            _ => "Consulta o portal para veres o estado atualizado.",
        };

        return new PushNotificationPayload(
            Title: title,
            Body: body,
            Url: $"/r/{rep.PublicSlug}",
            Tag: $"repair-{rep.PublicSlug}",
            Estado: rep.Estado);
    }

    private static bool IsExpiredSubscription(Exception ex)
        => ex is WebPushException webEx
            && (webEx.StatusCode == HttpStatusCode.Gone || webEx.StatusCode == HttpStatusCode.NotFound);
}
