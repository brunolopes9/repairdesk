using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class TenantBillingSettingsRepository : ITenantBillingSettingsRepository
{
    private readonly AppDbContext _db;

    public TenantBillingSettingsRepository(AppDbContext db) => _db = db;

    public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantBillingSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

    public async Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => await _db.TenantBillingSettings.AddAsync(settings, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
