using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Audit;

public interface IAuditService
{
    Task<PagedResult<AuditEntryDto>> SearchAsync(AuditSearchRequest req, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repo;
    private readonly ICurrentUser _user;

    public AuditService(IAuditRepository repo, ICurrentUser user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<PagedResult<AuditEntryDto>> SearchAsync(AuditSearchRequest req, CancellationToken ct = default)
    {
        if (req.IncludeAllTenants && !_user.IsInRole("SuperAdmin"))
            throw new ForbiddenException("super_admin_required", "includeAllTenants requer SuperAdmin.");

        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(
            req.EntityType,
            req.EntityId,
            req.From,
            req.To,
            req.IncludeAllTenants,
            page,
            pageSize,
            ct);
        return new PagedResult<AuditEntryDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    private static AuditEntryDto ToDto(AuditEntry a) =>
        new(a.Id, a.TenantId, a.AppUserId, a.Action, a.EntityType, a.EntityId, a.ChangesJson, a.IpAddress, a.UserAgent, a.CreatedAt);
}
