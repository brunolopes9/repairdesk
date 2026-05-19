using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Uma tentativa de delivery (ou conjunto de tentativas) de um evento para uma subscription.
/// Vive até <see cref="Status"/> ser <c>Delivered</c> ou <c>Failed</c> (esgotou retries).
/// </summary>
public class WebhookDelivery : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WebhookSubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }
    public required string EventType { get; set; }
    /// <summary>Payload completo enviado no body (JSON). Persistido para inspecção e replay.</summary>
    public required string PayloadJson { get; set; }
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public int? LastResponseCode { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRetryAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FailedAt { get; set; }
}

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,   // esgotou retries
}
