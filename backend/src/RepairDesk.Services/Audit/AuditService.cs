using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Common.Helpers;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Audit;

public interface IAuditService
{
    Task<PagedResult<AuditEntryDto>> SearchAsync(AuditSearchRequest req, CancellationToken ct = default);
    Task<AuditFilterOptionsDto> GetFilterOptionsAsync(bool includeAllTenants = false, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(AuditSearchRequest req, CancellationToken ct = default);
    Task<(byte[] Pdf, string Filename)> ExportPdfAsync(AuditSearchRequest req, CancellationToken ct = default);
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
        var (items, total) = await _repo.SearchAsync(ToQuery(req, page, pageSize), ct);
        return new PagedResult<AuditEntryDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<AuditFilterOptionsDto> GetFilterOptionsAsync(bool includeAllTenants = false, CancellationToken ct = default)
    {
        if (includeAllTenants && !_user.IsInRole("SuperAdmin"))
            throw new ForbiddenException("super_admin_required", "includeAllTenants requer SuperAdmin.");

        var options = await _repo.GetFilterOptionsAsync(includeAllTenants, ct);
        return new AuditFilterOptionsDto(
            options.EntityTypes,
            options.Users.Select(u => new AuditUserOptionDto(u.Id, u.DisplayName, u.Email)).ToList(),
            options.Actions,
            options.ApiKeys.Select(k => new AuditApiKeyOptionDto(k.Id, k.Name, k.KeyPrefix, k.Revoked)).ToList());
    }

    public async Task<byte[]> ExportCsvAsync(AuditSearchRequest req, CancellationToken ct = default)
    {
        var query = ToQuery(req, 1, 10_000);
        var (items, _) = await _repo.SearchAsync(query, ct);
        var csv = new CsvBuilder();
        csv.Row("quando", "utilizador", "email", "acao", "entidade", "entityId", "ip", "detalhe");
        foreach (var item in items)
        {
            csv.Row(
                item.CreatedAt,
                item.AppUser?.DisplayName ?? item.AppUserId?.ToString(),
                item.AppUser?.Email,
                item.Action.ToString(),
                item.EntityType,
                item.EntityId,
                item.IpAddress,
                item.ChangesJson);
        }
        return csv.ToUtf8WithBom();
    }

    public async Task<(byte[] Pdf, string Filename)> ExportPdfAsync(AuditSearchRequest req, CancellationToken ct = default)
    {
        var query = ToQuery(req, 1, 500);
        var (items, total) = await _repo.SearchAsync(query, ct);
        var dtos = items.Select(ToDto).ToList();
        var pdf = AuditPdfRenderer.Render(dtos, total, req.From, req.To);
        return (pdf, $"auditoria_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    private AuditQuery ToQuery(AuditSearchRequest req, int page, int pageSize)
    {
        if (req.IncludeAllTenants && !_user.IsInRole("SuperAdmin"))
            throw new ForbiddenException("super_admin_required", "includeAllTenants requer SuperAdmin.");

        return new AuditQuery(
            req.EntityTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            req.EntityId,
            req.UserIds.Distinct().ToList(),
            req.Actions.Distinct().ToList(),
            req.Search,
            req.From,
            req.To,
            req.IncludeAllTenants,
            page,
            pageSize,
            (req.ServiceApiKeyIds ?? Array.Empty<Guid>()).Distinct().ToList());
    }

    private static AuditEntryDto ToDto(AuditEntry a) =>
        new(a.Id, a.TenantId, a.AppUserId, a.AppUser?.DisplayName, a.AppUser?.Email,
            a.Action, a.EntityType, a.EntityId, a.ChangesJson, a.IpAddress, a.UserAgent, a.CreatedAt,
            a.ServiceApiKeyId, a.ServiceApiKey?.Name, a.ServiceApiKey?.KeyPrefix);
}
