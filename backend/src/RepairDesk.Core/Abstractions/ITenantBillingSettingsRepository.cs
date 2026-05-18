using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ITenantBillingSettingsRepository
{
    Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
