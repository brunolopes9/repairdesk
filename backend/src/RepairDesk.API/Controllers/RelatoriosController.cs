using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize]
public sealed class RelatoriosController : ControllerBase
{
    private readonly IRelatorioFiscalService _fiscal;
    private readonly IRelatorioNegocioService _negocio;

    public RelatoriosController(IRelatorioFiscalService fiscal, IRelatorioNegocioService negocio)
    {
        _fiscal = fiscal;
        _negocio = negocio;
    }

    [HttpGet("iva")]
    public Task<RelatorioIvaResponse> GetIva([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
        => _fiscal.GetIvaAsync(ano, trimestre, ivaComprasCents, ct);

    [HttpGet("negocio")]
    public Task<RelatorioNegocioResponse> GetNegocio([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
        => _negocio.GetAsync(ano, trimestre, ct);

    [HttpGet("iva/export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var bytes = await _fiscal.ExportIvaCsvAsync(ano, trimestre, ivaComprasCents, ct);
        return File(bytes, "text/csv; charset=utf-8", $"relatorio_iva_{ano}_T{trimestre}.csv");
    }

    [HttpGet("iva/export.pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var (pdf, filename) = await _fiscal.ExportIvaPdfAsync(ano, trimestre, ivaComprasCents, ct);
        return File(pdf, "application/pdf", filename);
    }
}
