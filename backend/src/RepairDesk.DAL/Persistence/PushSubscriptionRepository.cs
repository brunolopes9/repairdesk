using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly AppDbContext _db;

    public PushSubscriptionRepository(AppDbContext db) => _db = db;

    public Task<PushSubscription?> FindByEndpointAsync(Guid reparacaoId, string endpoint, CancellationToken ct = default)
        => _db.PushSubscriptions.FirstOrDefaultAsync(x => x.ReparacaoId == reparacaoId && x.Endpoint == endpoint, ct);

    public async Task<IReadOnlyList<PushSubscription>> ListByReparacaoIdAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.PushSubscriptions
            .Where(x => x.ReparacaoId == reparacaoId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PushSubscription>> ListDeliveredOlderThanAsync(DateTime deliveredBefore, CancellationToken ct = default)
        => await _db.PushSubscriptions
            .Include(x => x.Reparacao)
            .Where(x => x.Reparacao != null
                && x.Reparacao.Estado == RepairStatus.Entregue
                && x.Reparacao.EntregueEm != null
                && x.Reparacao.EntregueEm < deliveredBefore)
            .ToListAsync(ct);

    public async Task AddAsync(PushSubscription subscription, CancellationToken ct = default)
        => await _db.PushSubscriptions.AddAsync(subscription, ct);

    public void Remove(PushSubscription subscription) => _db.PushSubscriptions.Remove(subscription);

    public void RemoveRange(IEnumerable<PushSubscription> subscriptions) => _db.PushSubscriptions.RemoveRange(subscriptions);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
