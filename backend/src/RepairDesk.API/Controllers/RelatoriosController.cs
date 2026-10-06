using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize]
public sealed class RelatoriosController : ControllerBase
{
    private readonly IRelatorioNegocioService _negocio;

    public RelatoriosController(IRelatorioNegocioService negocio)
    {
        _negocio = negocio;
    }

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
}
