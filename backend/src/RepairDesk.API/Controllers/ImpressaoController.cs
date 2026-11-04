using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Impressao;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 593: imprimir de uma vez o que está numa lista (vendas, compras, despesas), com os mesmos filtros do
/// ecrã. Devolve um só PDF: a lista com totais e, a seguir, as faturas. <c>conteudo</c>: tudo | faturas | lista.
/// </summary>
[ApiController]
[Route("api/imprimir")]
[Authorize]
public class ImpressaoController : ControllerBase
{
    private readonly IImpressaoService _impressao;
    public ImpressaoController(IImpressaoService impressao) => _impressao = impressao;

    [HttpGet("vendas")]
    public async Task<IActionResult> Vendas(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] VendaTipo? tipo,
        [FromQuery] VendaEstado? estado,
        [FromQuery] bool emCurso = false,
        [FromQuery] bool faturaPorRegistar = false,
        [FromQuery] bool porPagar = false,
        [FromQuery] string? q = null,
        [FromQuery] string? atividade = null,
        [FromQuery] string? conteudo = null,
        CancellationToken ct = default)
    {
        var filtro = new VendaFiltro(from, to, null, tipo, estado, emCurso, faturaPorRegistar, q, null, porPagar,
            atividade?.ToLowerInvariant() switch { "trading" => VendaAtividade.Trading, "todas" => null, _ => VendaAtividade.Loja });
        var (pdf, nome) = await _impressao.VendasAsync(filtro, Conteudo(conteudo), ct);
        return File(pdf, "application/pdf", nome);
    }

    [HttpGet("compras")]
    public async Task<IActionResult> Compras(
        [FromQuery] string? q,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] bool faturaEmFalta = false,
        [FromQuery] DateTime? de = null,
        [FromQuery] DateTime? ate = null,
        [FromQuery] string? conteudo = null,
        CancellationToken ct = default)
    {
        var filtro = new CompraFiltro(q, fornecedorId, faturaEmFalta,
            de is { } d ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null,
            ate is { } a ? DateTime.SpecifyKind(a.Date.AddDays(1), DateTimeKind.Utc) : null);
        var (pdf, nome) = await _impressao.ComprasAsync(filtro, Conteudo(conteudo), ct);
        return File(pdf, "application/pdf", nome);
    }

    [HttpGet("despesas")]
    public async Task<IActionResult> Despesas(
        [FromQuery] string? q,
        [FromQuery] DespesaCategoria? categoria,
        [FromQuery] bool? isRecorrente,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? conteudo = null,
        CancellationToken ct = default)
    {
        var (pdf, nome) = await _impressao.DespesasAsync(new DespesaFiltroImpressao(q, categoria, isRecorrente, from, to), Conteudo(conteudo), ct);
        return File(pdf, "application/pdf", nome);
    }

    private static ConteudoImpressao Conteudo(string? c) => c?.ToLowerInvariant() switch
    {
        "faturas" => ConteudoImpressao.Faturas,
        "lista" => ConteudoImpressao.Lista,
        _ => ConteudoImpressao.Tudo,
    };
}
