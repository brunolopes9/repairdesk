using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Audit;

public sealed record AuditEntryDto(
    Guid Id,
    Guid TenantId,
    Guid? AppUserId,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    string? ChangesJson,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

public sealed record AuditSearchRequest(
    string? EntityType,
    Guid? EntityId,
    DateTime? From,
    DateTime? To,
    bool IncludeAllTenants,
    int Page,
    int PageSize);
