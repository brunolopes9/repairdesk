using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class WhatsAppNotificationLogRepository : IWhatsAppNotificationLogRepository
{
    private readonly AppDbContext _db;

    public WhatsAppNotificationLogRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid entityId, string templateKey, string entityType = "Reparacao", CancellationToken ct = default)
        => _db.WhatsAppNotificationLogs
            .AsNoTracking()
            .AnyAsync(x => x.EntityId == entityId
                           && x.EntityType == entityType
                           && x.TemplateKey == templateKey, ct);

    public async Task AddAsync(WhatsAppNotificationLog log, CancellationToken ct = default)
        => await _db.WhatsAppNotificationLogs.AddAsync(log, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
