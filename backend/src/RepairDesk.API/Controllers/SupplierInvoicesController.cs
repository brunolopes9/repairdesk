using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 148: endpoints admin (JWT-auth) para Bruno gerir as importações de faturas
/// pendentes — listar, ver PDF, aprovar (cria Despesa) ou rejeitar. Plus export ZIP
/// trimestral para entregar ao contabilista.
/// </summary>
[ApiController]
[Route("api/supplier-invoices")]
[Authorize]
public class SupplierInvoicesController : ControllerBase
{
    private readonly ISupplierInvoiceImportService _service;

    public SupplierInvoicesController(ISupplierInvoiceImportService service) => _service = service;

    [HttpGet("pending")]
    public Task<IReadOnlyList<SupplierInvoiceImportDto>> Pending([FromQuery] int take = 100, CancellationToken ct = default)
        => _service.ListPendingAsync(take, ct);

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var bytes = await _service.GetPdfAsync(id, ct);
        return File(bytes, "application/pdf");
    }

    [HttpPost("{id:guid}/approve")]
    public Task<SupplierInvoiceImportDto> Approve(Guid id, [FromBody] ApproveSupplierInvoiceRequest req, CancellationToken ct)
        => _service.ApproveAsync(id, req, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<SupplierInvoiceImportDto> Reject(Guid id, [FromBody] RejectSupplierInvoiceRequest req, CancellationToken ct)
        => _service.RejectAsync(id, req.Reason, ct);

    /// <summary>
    /// Sprint 160: aprovar items como Stock. Por linha Bruno escolhe acção
    /// (existing/new/skip). Cria/incrementa Parts + PartMovimentos.
    /// SkuMapping aprende para próximas importações.
    /// </summary>
    [HttpPost("{id:guid}/approve-stock")]
    public Task<SupplierInvoiceImportDto> ApproveStock(Guid id, [FromBody] ApproveAsStockRequest req, CancellationToken ct)
        => _service.ApproveAsStockAsync(id, req, ct);

    /// <summary>
    /// Sprint 148: export ZIP de todas as facturas APROVADAS no período. Útil para entregar
    /// ao contabilista no fim do trimestre. Estrutura interna: yyyy/MM/supplier/filename.pdf.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct)
    {
        if (to <= from) return BadRequest(new { code = "invalid_range", detail = "to deve ser depois de from." });
        if ((to - from).TotalDays > 400) return BadRequest(new { code = "range_too_large", detail = "Período máximo 400 dias." });
        var (zip, filename) = await _service.ExportZipAsync(from, to, ct);
        return File(zip, "application/zip", filename);
    }
}

public sealed record RejectSupplierInvoiceRequest(string? Reason);
