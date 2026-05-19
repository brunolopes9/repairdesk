using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Webhooks;

/// <summary>
/// Publica um evento — encontra todas as subscriptions activas do tenant que ouvem
/// este event type e enfileira uma <see cref="WebhookDelivery"/> Pending para cada uma.
/// O <c>WebhookDeliveryHostedService</c> apanha-as depois.
/// </summary>
public interface IWebhookPublisher
{
    /// <param name="payload">Objecto serializado para o body do POST.</param>
    Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default);
}

public class WebhookPublisher : IWebhookPublisher
{
    private readonly IWebhookSubscriptionRepository _subs;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly ILogger<WebhookPublisher> _logger;

    public WebhookPublisher(
        IWebhookSubscriptionRepository subs,
        IWebhookDeliveryRepository deliveries,
        ILogger<WebhookPublisher> logger)
    {
        _subs = subs;
        _deliveries = deliveries;
        _logger = logger;
    }

    public async Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        var matching = await _subs.ListActiveForEventAsync(tenantId, eventType, ct);
        if (matching.Count == 0) return;

        var envelope = new
        {
            id = Guid.NewGuid(),
            @event = eventType,
            tenantId,
            createdAt = DateTime.UtcNow,
            data = payload,
        };
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        foreach (var sub in matching)
        {
            await _deliveries.AddAsync(new WebhookDelivery
            {
                TenantId = tenantId,
                WebhookSubscriptionId = sub.Id,
                EventType = eventType,
                PayloadJson = json,
                Status = WebhookDeliveryStatus.Pending,
                NextRetryAt = DateTime.UtcNow,
            }, ct);
        }
        await _deliveries.SaveAsync(ct);

        _logger.LogInformation("Webhook event {EventType} enqueued for {Count} subscription(s) of tenant {TenantId}",
            eventType, matching.Count, tenantId);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
