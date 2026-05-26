using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IStaffPushSubscriptionRepository
{
    Task<StaffPushSubscription?> FindByEndpointAsync(Guid userId, string endpoint, CancellationToken ct = default);
    /// <summary>Todas as subscrições activas de um tenant (todos os dispositivos de todos os staff).</summary>
    Task<IReadOnlyList<StaffPushSubscription>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(StaffPushSubscription subscription, CancellationToken ct = default);
    void Remove(StaffPushSubscription subscription);
    Task SaveAsync(CancellationToken ct = default);
}
