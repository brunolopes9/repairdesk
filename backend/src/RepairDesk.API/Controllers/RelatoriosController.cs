using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize]
public sealed class RelatoriosController : ControllerBase
{
    private readonly IRelatorioFiscalService _service;

    public RelatoriosController(IRelatorioFiscalService service) => _service = service;

    [HttpGet("iva")]
    public Task<RelatorioIvaResponse> GetIva([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
        => _service.GetIvaAsync(ano, trimestre, ivaComprasCents, ct);

    [HttpGet("iva/export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var bytes = await _service.ExportIvaCsvAsync(ano, trimestre, ivaComprasCents, ct);
        return File(bytes, "text/csv; charset=utf-8", $"relatorio_iva_{ano}_T{trimestre}.csv");
    }

    [HttpGet("iva/export.pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var (pdf, filename) = await _service.ExportIvaPdfAsync(ano, trimestre, ivaComprasCents, ct);
        return File(pdf, "application/pdf", filename);
    }
}
