using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> ListByTenantAsync(CancellationToken ct = default);
    Task<WebhookSubscription?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Lista subscriptions activas que estão a ouvir um event type específico (filtro server-side).</summary>
    Task<IReadOnlyList<WebhookSubscription>> ListActiveForEventAsync(Guid tenantId, string eventType, CancellationToken ct = default);
    Task AddAsync(WebhookSubscription sub, CancellationToken ct = default);
    void Remove(WebhookSubscription sub);
    Task SaveAsync(CancellationToken ct = default);
}
