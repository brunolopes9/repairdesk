using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IAuditRepository
{
    Task<(IReadOnlyList<AuditEntry> Items, int Total)> SearchAsync(
        AuditQuery query,
        CancellationToken ct = default);

    Task<AuditFilterOptionsSnapshot> GetFilterOptionsAsync(bool includeAllTenants, CancellationToken ct = default);
}

public sealed record AuditQuery(
    IReadOnlyList<string> EntityTypes,
    Guid? EntityId,
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<AuditAction> Actions,
    string? Search,
    DateTime? From,
    DateTime? To,
    bool IncludeAllTenants,
    int Page,
    int PageSize,
    IReadOnlyList<Guid> ServiceApiKeyIds);

public sealed record AuditFilterOptionsSnapshot(
    IReadOnlyList<string> EntityTypes,
    IReadOnlyList<AuditUserOptionRow> Users,
    IReadOnlyList<AuditAction> Actions,
    IReadOnlyList<AuditApiKeyOptionRow> ApiKeys);

public sealed record AuditUserOptionRow(Guid Id, string DisplayName, string? Email);

public sealed record AuditApiKeyOptionRow(Guid Id, string Name, string KeyPrefix, bool Revoked);
