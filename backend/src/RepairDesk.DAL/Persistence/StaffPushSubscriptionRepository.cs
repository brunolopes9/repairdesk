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

    public async Task<IReadOnlyList<StaffPushSubscription>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.StaffPushSubscriptions
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StaffPushSubscription>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.StaffPushSubscriptions
            .IgnoreQueryFilters() // chamado a partir de um worker sem tenant no contexto
            // Sprint 627: quem trabalha em várias empresas (LopesTech + Cortex) recebe os avisos de todas,
            // seja qual for a empresa que estava ativa quando ativou as notificações no dispositivo.
            .Where(x => !x.IsDeleted && (x.TenantId == tenantId
                || _db.Users.IgnoreQueryFilters().Any(u => u.Id == x.UserId && u.TenantId == tenantId)
                || _db.AcessosEmpresa.IgnoreQueryFilters().Any(a => a.UserId == x.UserId && a.TenantId == tenantId)))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(StaffPushSubscription subscription, CancellationToken ct = default)
        => await _db.StaffPushSubscriptions.AddAsync(subscription, ct);

    public void Remove(StaffPushSubscription subscription) => _db.StaffPushSubscriptions.Remove(subscription);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
