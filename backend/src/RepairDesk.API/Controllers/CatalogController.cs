using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Catalog;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 385 (Doc 87): vista unificada "Catálogo &amp; Stock" — junta produtos retail
/// (ProductModel/Product) e stock técnico (Part) numa árvore pai→variante, com KPIs e tabs.
/// READ-ONLY. Operacional (Admin + Tech) — qualquer empregado pode consultar o catálogo.
/// </summary>
[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogService _service;
    public CatalogController(ICatalogService service) => _service = service;

    /// <summary>Lista linhas-pai (filtradas) + KPIs globais do catálogo.</summary>
    [HttpGet]
    public Task<CatalogListDto> List(
        [FromQuery] string? q,
        [FromQuery] string? categoria,
        [FromQuery] string? marca,
        [FromQuery] string? fornecedor,
        [FromQuery] string? estado,
        [FromQuery] string tab = "todos",
        CancellationToken ct = default)
        => _service.ListAsync(new CatalogQuery(q, categoria, marca, fornecedor, estado, tab), ct);

    /// <summary>KPIs do catálogo (atalho — o GET principal já os inclui).</summary>
    [HttpGet("kpis")]
    public async Task<CatalogKpisDto> Kpis(CancellationToken ct = default)
        => (await _service.ListAsync(new CatalogQuery(), ct)).Kpis;

    /// <summary>Sprint 388: liga/desliga a visibilidade na loja de uma variante (kind = product|part).</summary>
    [HttpPost("variant/{kind}/{id:guid}/loja-online")]
    public async Task<IActionResult> SetLojaOnline(string kind, Guid id, [FromQuery] bool value, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.SetVariantLojaOnlineAsync(kind, id, value, ct);
            return Ok(new { lojaOnline = result });
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { error = e.Message }); }
    }
}
