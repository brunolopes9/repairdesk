using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 613 (Doc 97): produtos da loja do lopestech.pt — só o dono (Admin).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/loja/produtos")]
public sealed class LojaController : ControllerBase
{
    private readonly ILojaService _service;
    public LojaController(ILojaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProdutoLojaDto>>> List(CancellationToken ct) => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProdutoLojaDto>> Get(Guid id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProdutoLojaDto>> Create([FromBody] GuardarProdutoLojaRequest req, CancellationToken ct)
    {
        var p = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    /// <summary>Sprint 636: vários produtos de uma vez, como rascunho (ex.: a montra a partir da fatura do fornecedor).</summary>
    [HttpPost("importar")]
    public async Task<ActionResult<ImportarProdutosResultado>> Importar([FromBody] IReadOnlyList<GuardarProdutoLojaRequest> produtos, CancellationToken ct)
        => Ok(await _service.ImportarAsync(produtos, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProdutoLojaDto>> Update(Guid id, [FromBody] GuardarProdutoLojaRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/publicar")]
    public async Task<ActionResult<ProdutoLojaDto>> Publicar(Guid id, CancellationToken ct) => Ok(await _service.PublicarAsync(id, true, ct));

    [HttpPost("{id:guid}/despublicar")]
    public async Task<ActionResult<ProdutoLojaDto>> Despublicar(Guid id, CancellationToken ct) => Ok(await _service.PublicarAsync(id, false, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/fotos/upload")]
    public async Task<ActionResult<UploadFotoTicketDto>> PedirUpload(Guid id, [FromBody] PedirUploadFotoRequest req, CancellationToken ct)
        => Ok(await _service.PedirUploadFotoAsync(id, req, ct));

    [HttpPost("{id:guid}/fotos/{fotoId:guid}/confirmar")]
    public async Task<ActionResult<ProdutoLojaDto>> ConfirmarFoto(Guid id, Guid fotoId, CancellationToken ct)
        => Ok(await _service.ConfirmarFotoAsync(id, fotoId, ct));

    [HttpPut("{id:guid}/fotos/ordem")]
    public async Task<ActionResult<ProdutoLojaDto>> OrdenarFotos(Guid id, [FromBody] IReadOnlyList<Guid> ordem, CancellationToken ct)
        => Ok(await _service.OrdenarFotosAsync(id, ordem, ct));

    [HttpDelete("{id:guid}/fotos/{fotoId:guid}")]
    public async Task<ActionResult<ProdutoLojaDto>> ApagarFoto(Guid id, Guid fotoId, CancellationToken ct)
        => Ok(await _service.ApagarFotoAsync(id, fotoId, ct));
}

/// <summary>
/// Sprint 613 (Doc 97): catálogo publicado para o gerador do lopestech.pt (API key com scope «loja»).
/// Só leitura e sem custos/margens.
/// </summary>
[ApiController]
[Route("api/external/loja")]
[Authorize(AuthenticationSchemes = ApiKeyAuthHandler.SchemeName)]
[ApiScope("loja")]
[EnableRateLimiting("external-apikey")]
public sealed class ExternalLojaController : ControllerBase
{
    private readonly ILojaService _service;
    public ExternalLojaController(ILojaService service) => _service = service;

    [HttpGet("catalogo")]
    public async Task<ActionResult<CatalogoDto>> Catalogo(CancellationToken ct) => Ok(await _service.CatalogoAsync(ct));
}
