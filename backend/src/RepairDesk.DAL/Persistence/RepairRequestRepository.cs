using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class RepairRequestRepository : IRepairRequestRepository
{
    private readonly AppDbContext _db;
    public RepairRequestRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RepairRequest>> ListAsync(RepairRequestEstado? estado, CancellationToken ct = default)
    {
        var q = _db.RepairRequests.AsQueryable();
        if (estado is not null) q = q.Where(r => r.Estado == estado);
        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountPendentesAsync(CancellationToken ct = default) =>
        _db.RepairRequests.CountAsync(r => r.Estado == RepairRequestEstado.Pendente, ct);

    public Task<RepairRequest?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.RepairRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(RepairRequest req, CancellationToken ct = default)
    {
        await _db.RepairRequests.AddAsync(req, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<int> CountRecentByIpAsync(Guid tenantId, string sourceIp, TimeSpan window, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - window;
        // IgnoreQueryFilters: o POST público não tem tenant context, filtramos manualmente.
        return _db.RepairRequests
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId && r.SourceIp == sourceIp && r.CreatedAt >= since, ct);
    }
}
