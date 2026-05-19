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

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
