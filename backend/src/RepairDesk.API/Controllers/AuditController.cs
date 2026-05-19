using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Clientes;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _service;

    public AuditController(IAuditService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<AuditEntryDto>> Search(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeAllTenants = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(new AuditSearchRequest(
            ToList(entityType),
            entityId,
            Array.Empty<Guid>(),
            Array.Empty<AuditAction>(),
            null,
            from,
            to,
            includeAllTenants,
            page,
            pageSize), ct);

    [HttpGet("search")]
    public Task<PagedResult<AuditEntryDto>> SearchRich(
        [FromQuery] string[]? entityTypes,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid[]? userIds,
        [FromQuery] AuditAction[]? actions,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeAllTenants = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid[]? serviceApiKeyIds = null,
        CancellationToken ct = default)
        => _service.SearchAsync(new AuditSearchRequest(
            entityTypes ?? Array.Empty<string>(),
            entityId,
            userIds ?? Array.Empty<Guid>(),
            actions ?? Array.Empty<AuditAction>(),
            search,
            from,
            to,
            includeAllTenants,
            page,
            pageSize,
            serviceApiKeyIds ?? Array.Empty<Guid>()), ct);

    [HttpGet("filters")]
    public Task<AuditFilterOptionsDto> Filters([FromQuery] bool includeAllTenants = false, CancellationToken ct = default)
        => _service.GetFilterOptionsAsync(includeAllTenants, ct);

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string[]? entityTypes,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid[]? userIds,
        [FromQuery] AuditAction[]? actions,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeAllTenants = false,
        [FromQuery] Guid[]? serviceApiKeyIds = null,
        CancellationToken ct = default)
    {
        var bytes = await _service.ExportCsvAsync(new AuditSearchRequest(
            entityTypes ?? Array.Empty<string>(),
            entityId,
            userIds ?? Array.Empty<Guid>(),
            actions ?? Array.Empty<AuditAction>(),
            search,
            from,
            to,
            includeAllTenants,
            1,
            10_000,
            serviceApiKeyIds ?? Array.Empty<Guid>()), ct);
        return File(bytes, "text/csv; charset=utf-8", $"auditoria_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    [HttpGet("export.pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] string[]? entityTypes,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid[]? userIds,
        [FromQuery] AuditAction[]? actions,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeAllTenants = false,
        [FromQuery] Guid[]? serviceApiKeyIds = null,
        CancellationToken ct = default)
    {
        var (pdf, filename) = await _service.ExportPdfAsync(new AuditSearchRequest(
            entityTypes ?? Array.Empty<string>(),
            entityId,
            userIds ?? Array.Empty<Guid>(),
            actions ?? Array.Empty<AuditAction>(),
            search,
            from,
            to,
            includeAllTenants,
            1,
            500,
            serviceApiKeyIds ?? Array.Empty<Guid>()), ct);
        return File(pdf, "application/pdf", filename);
    }

    private static IReadOnlyList<string> ToList(string? value)
        => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value };
}
