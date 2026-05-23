using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ITenantPreferencesRepository
{
    Task<TenantPreferences?> FindByTenantIdAsync(Guid tenantId, bool ignoreQueryFilters = false, CancellationToken ct = default);
    Task AddAsync(TenantPreferences preferences, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
