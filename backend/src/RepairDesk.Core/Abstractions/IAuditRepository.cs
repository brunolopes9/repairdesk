using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IAuditRepository
{
    Task<(IReadOnlyList<AuditEntry> Items, int Total)> SearchAsync(
        string? entityType,
        Guid? entityId,
        DateTime? from,
        DateTime? to,
        bool includeAllTenants,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
