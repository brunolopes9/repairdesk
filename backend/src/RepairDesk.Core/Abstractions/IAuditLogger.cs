using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        object? changes = null,
        Guid? tenantId = null,
        Guid? appUserId = null,
        CancellationToken ct = default);
}
