using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class WebhookSubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly AppDbContext _db;
    public WebhookSubscriptionRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebhookSubscription>> ListByTenantAsync(CancellationToken ct = default)
        => await _db.WebhookSubscriptions
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public Task<WebhookSubscription?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.WebhookSubscriptions.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<WebhookSubscription>> ListActiveForEventAsync(Guid tenantId, string eventType, CancellationToken ct = default)
    {
        // O filtro CSV é feito em memória depois de fetch — listas de subscriptions são
        // sempre pequenas (uma loja online tem 1-2 subscriptions, não centenas). Evita
        // SQL LIKE complexo e mantém o predicado legível.
        var subs = await _db.WebhookSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Active && !w.IsDeleted && w.DisabledAt == null)
            .ToListAsync(ct);
        return subs.Where(w => w.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                       .Contains(eventType, StringComparer.OrdinalIgnoreCase))
                   .ToList();
    }

    public Task AddAsync(WebhookSubscription sub, CancellationToken ct = default)
        => _db.WebhookSubscriptions.AddAsync(sub, ct).AsTask();

    public void Remove(WebhookSubscription sub) => _db.WebhookSubscriptions.Remove(sub);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
