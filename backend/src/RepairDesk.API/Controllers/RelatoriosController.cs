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
    private readonly IExtratoService _extrato;

    public RelatoriosController(IRelatorioFiscalService fiscal, IRelatorioNegocioService negocio, IExtratoService extrato)
    {
        _fiscal = fiscal;
        _negocio = negocio;
        _extrato = extrato;
    }

    [HttpGet("iva")]
    public Task<RelatorioIvaResponse> GetIva([FromQuery] int ano, [FromQuery] int trimestre, [FromQuery] int ivaComprasCents = 0, CancellationToken ct = default)
        => _fiscal.GetIvaAsync(ano, trimestre, ivaComprasCents, ct);

    [HttpGet("negocio")]
    public Task<RelatorioNegocioResponse> GetNegocio([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
        => _negocio.GetAsync(ano, trimestre, ct);

    // Sprint 187: análise B2B do desempenho de cada fornecedor — quantos dos artigos vendidos
    // voltaram para reparação. Janela deslizante (default 12 meses) porque defeitos manifestam-se
    // ao longo de muitos meses pós-venda, ao contrário das outras métricas trimestrais.
    [HttpGet("taxa-defeito-fornecedor")]
    public Task<TaxaDefeitoFornecedorResponse> GetTaxaDefeitoFornecedor([FromQuery] int meses = 12, CancellationToken ct = default)
        => _negocio.GetTaxaDefeitoFornecedorAsync(meses, ct);

    // Sprint 547 (Doc 93 #2): Análise de Vendas — top artigos + top clientes do trimestre.
    [HttpGet("analise-vendas")]
    public Task<AnaliseVendasResponse> GetAnaliseVendas([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
        => _negocio.GetAnaliseVendasAsync(ano, trimestre, ct);

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

    // Sprint 542: Extrato unificado (Vendas lista única incl. Moloni + Compras stock + Despesas OpEx)
    // por data, em PDF — o documento que se entrega ao contabilista. 'to' é INCLUSIVO (dia inteiro).
    [HttpGet("extrato/export.pdf")]
    public async Task<IActionResult> ExtratoPdf([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var (pdf, filename) = await _extrato.ExportPdfAsync(
            DateTime.SpecifyKind(from.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc),
            ct);
        return File(pdf, "application/pdf", filename);
    }
}
