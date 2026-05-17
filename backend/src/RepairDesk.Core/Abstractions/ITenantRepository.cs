using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
