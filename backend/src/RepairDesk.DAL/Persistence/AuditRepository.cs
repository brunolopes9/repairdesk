using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditEntry> Items, int Total)> SearchAsync(
        string? entityType,
        Guid? entityId,
        DateTime? from,
        DateTime? to,
        bool includeAllTenants,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = includeAllTenants
            ? _db.AuditEntries.IgnoreQueryFilters().AsNoTracking()
            : _db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);
        if (entityId is not null)
            q = q.Where(a => a.EntityId == entityId);
        if (from is not null)
            q = q.Where(a => a.CreatedAt >= from.Value);
        if (to is not null)
            q = q.Where(a => a.CreatedAt <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}
