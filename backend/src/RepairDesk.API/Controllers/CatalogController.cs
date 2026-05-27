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
}
