using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IStaffPushSubscriptionRepository
{
    Task<StaffPushSubscription?> FindByEndpointAsync(Guid userId, string endpoint, CancellationToken ct = default);
    /// <summary>Todas as subscrições activas de um tenant (todos os dispositivos de todos os staff).</summary>
    Task<IReadOnlyList<StaffPushSubscription>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Sprint 623: os dispositivos de um utilizador, qualquer que seja a empresa em que os ativou (avisos pessoais).</summary>
    Task<IReadOnlyList<StaffPushSubscription>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(StaffPushSubscription subscription, CancellationToken ct = default);
    void Remove(StaffPushSubscription subscription);
    Task SaveAsync(CancellationToken ct = default);
}
