using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly AppDbContext _db;
    public WebhookDeliveryRepository(AppDbContext db) => _db = db;

    public Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
        => _db.WebhookDeliveries.AddAsync(delivery, ct).AsTask();

    public async Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(int max, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.WebhookDeliveries
            .IgnoreQueryFilters()   // background processor não tem tenant context
            .Include(d => d.Subscription)
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextRetryAt != null && d.NextRetryAt <= now)
            .OrderBy(d => d.NextRetryAt)
            .Take(max)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListBySubscriptionAsync(Guid subscriptionId, int take, CancellationToken ct = default)
        => await _db.WebhookDeliveries
            .AsNoTracking()
            .Where(d => d.WebhookSubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<WebhookDelivery?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<WebhookStatsRow> GetStatsAsync(DateTime since, CancellationToken ct = default)
    {
        // Tenant scoped pelo query filter — operador admin a olhar para o seu dashboard.
        var activeSubs = await _db.WebhookSubscriptions.CountAsync(s => s.Active && s.DisabledAt == null, ct);
        var disabledSubs = await _db.WebhookSubscriptions.CountAsync(s => s.DisabledAt != null, ct);

        var sinceWindow = _db.WebhookDeliveries.Where(d => d.CreatedAt >= since);
        var total = await sinceWindow.CountAsync(ct);
        var delivered = await sinceWindow.CountAsync(d => d.Status == WebhookDeliveryStatus.Delivered, ct);
        var failed = await sinceWindow.CountAsync(d => d.Status == WebhookDeliveryStatus.Failed, ct);

        var pendingNow = await _db.WebhookDeliveries.CountAsync(d => d.Status == WebhookDeliveryStatus.Pending, ct);
        var lastAt = await _db.WebhookDeliveries
            .Where(d => d.DeliveredAt != null)
            .OrderByDescending(d => d.DeliveredAt)
            .Select(d => (DateTime?)d.DeliveredAt)
            .FirstOrDefaultAsync(ct);

        return new WebhookStatsRow(activeSubs, disabledSubs, total, delivered, failed, pendingNow, lastAt);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
