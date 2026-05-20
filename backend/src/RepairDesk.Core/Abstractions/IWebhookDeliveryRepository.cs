using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);
    /// <summary>Devolve entregas Pending cujo NextRetryAt expirou — para o processor.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(int max, CancellationToken ct = default);
    /// <summary>Últimas N entregas de uma subscription, mais recentes primeiro. Para debug UI.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListBySubscriptionAsync(Guid subscriptionId, int take, CancellationToken ct = default);
    Task<WebhookDelivery?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Stats agregadas para o Dashboard widget (tenant scoped via query filter).</summary>
    Task<WebhookStatsRow> GetStatsAsync(DateTime since, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public sealed record WebhookStatsRow(
    int ActiveSubscriptions,
    int DisabledSubscriptions,
    int DeliveriesSinceWindow,
    int DeliveredSinceWindow,
    int FailedSinceWindow,
    int PendingNow,
    DateTime? LastDeliveryAt);
