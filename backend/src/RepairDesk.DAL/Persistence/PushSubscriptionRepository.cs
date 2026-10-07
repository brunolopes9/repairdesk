using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly AppDbContext _db;

    public PushSubscriptionRepository(AppDbContext db) => _db = db;

    public Task<PushSubscription?> FindByEndpointAsync(Guid vendaId, string endpoint, CancellationToken ct = default)
        => _db.PushSubscriptions.FirstOrDefaultAsync(x => x.VendaId == vendaId && x.Endpoint == endpoint, ct);

    public async Task<IReadOnlyList<PushSubscription>> ListByVendaIdAsync(Guid vendaId, CancellationToken ct = default)
        => await _db.PushSubscriptions
            .Where(x => x.VendaId == vendaId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PushSubscription>> ListDeliveredOlderThanAsync(DateTime deliveredBefore, CancellationToken ct = default)
        => await _db.PushSubscriptions
            .Include(x => x.Venda)
            .Where(x => x.Venda != null
                && x.Venda.Estado == VendaEstado.Entregue
                && x.Venda.Data < deliveredBefore)
            .ToListAsync(ct);

    public async Task AddAsync(PushSubscription subscription, CancellationToken ct = default)
        => await _db.PushSubscriptions.AddAsync(subscription, ct);

    public void Remove(PushSubscription subscription) => _db.PushSubscriptions.Remove(subscription);

    public void RemoveRange(IEnumerable<PushSubscription> subscriptions) => _db.PushSubscriptions.RemoveRange(subscriptions);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
