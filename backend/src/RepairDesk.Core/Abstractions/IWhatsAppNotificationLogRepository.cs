using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IWhatsAppNotificationLogRepository
{
    Task<bool> ExistsAsync(Guid entityId, string templateKey, string entityType = "Reparacao", CancellationToken ct = default);
    Task AddAsync(WhatsAppNotificationLog log, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
