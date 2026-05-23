using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class TenantPreferencesRepository : ITenantPreferencesRepository
{
    private readonly AppDbContext _db;

    public TenantPreferencesRepository(AppDbContext db) => _db = db;

    public Task<TenantPreferences?> FindByTenantIdAsync(Guid tenantId, bool ignoreQueryFilters = false, CancellationToken ct = default)
    {
        var query = ignoreQueryFilters
            ? _db.TenantPreferences.IgnoreQueryFilters()
            : _db.TenantPreferences;

        return query.FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
    }

    public async Task AddAsync(TenantPreferences preferences, CancellationToken ct = default)
        => await _db.TenantPreferences.AddAsync(preferences, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
