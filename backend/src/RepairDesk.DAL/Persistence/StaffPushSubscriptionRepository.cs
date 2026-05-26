using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class StaffPushSubscriptionRepository : IStaffPushSubscriptionRepository
{
    private readonly AppDbContext _db;

    public StaffPushSubscriptionRepository(AppDbContext db) => _db = db;

    public Task<StaffPushSubscription?> FindByEndpointAsync(Guid userId, string endpoint, CancellationToken ct = default)
        => _db.StaffPushSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId && x.Endpoint == endpoint, ct);

    public async Task<IReadOnlyList<StaffPushSubscription>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.StaffPushSubscriptions
            .IgnoreQueryFilters() // chamado a partir de um worker sem tenant no contexto
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(StaffPushSubscription subscription, CancellationToken ct = default)
        => await _db.StaffPushSubscriptions.AddAsync(subscription, ct);

    public void Remove(StaffPushSubscription subscription) => _db.StaffPushSubscriptions.Remove(subscription);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
