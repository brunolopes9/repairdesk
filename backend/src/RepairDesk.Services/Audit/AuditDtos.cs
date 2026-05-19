using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Audit;

public sealed record AuditEntryDto(
    Guid Id,
    Guid TenantId,
    Guid? AppUserId,
    string? AppUserDisplayName,
    string? AppUserEmail,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    string? ChangesJson,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    /// <summary>Quando a acção foi feita por integração externa, identifica a chave.</summary>
    Guid? ServiceApiKeyId,
    string? ServiceApiKeyName,
    string? ServiceApiKeyPrefix);

public sealed record AuditSearchRequest(
    IReadOnlyList<string> EntityTypes,
    Guid? EntityId,
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<AuditAction> Actions,
    string? Search,
    DateTime? From,
    DateTime? To,
    bool IncludeAllTenants,
    int Page,
    int PageSize);

public sealed record AuditFilterOptionsDto(
    IReadOnlyList<string> EntityTypes,
    IReadOnlyList<AuditUserOptionDto> Users,
    IReadOnlyList<AuditAction> Actions);

public sealed record AuditUserOptionDto(Guid Id, string DisplayName, string? Email);
