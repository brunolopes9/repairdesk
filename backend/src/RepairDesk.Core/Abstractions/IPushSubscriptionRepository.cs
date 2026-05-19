using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IPushSubscriptionRepository
{
    Task<PushSubscription?> FindByEndpointAsync(Guid reparacaoId, string endpoint, CancellationToken ct = default);
    Task<IReadOnlyList<PushSubscription>> ListByReparacaoIdAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<IReadOnlyList<PushSubscription>> ListDeliveredOlderThanAsync(DateTime deliveredBefore, CancellationToken ct = default);
    Task AddAsync(PushSubscription subscription, CancellationToken ct = default);
    void Remove(PushSubscription subscription);
    void RemoveRange(IEnumerable<PushSubscription> subscriptions);
    Task SaveAsync(CancellationToken ct = default);
}
