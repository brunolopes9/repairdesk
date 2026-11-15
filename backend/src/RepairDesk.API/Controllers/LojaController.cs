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
    private readonly ITextoProdutoIa _ia;
    public LojaController(ILojaService service, ITextoProdutoIa ia)
    {
        _service = service;
        _ia = ia;
    }

    /// <summary>Sprint 672: a IA (subscrição) escreve descrição, especificações e textos do Google a partir do formulário.</summary>
    [HttpPost("gerar-texto")]
    public async Task<ActionResult<TextoProdutoGerado>> GerarTexto([FromBody] GuardarProdutoLojaRequest req, CancellationToken ct)
        => Ok(await _ia.GerarAsync(req, ct));

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
    private readonly RepairDesk.Core.Abstractions.ILojaMudancas _mudancas;
    public ExternalLojaController(ILojaService service, RepairDesk.Core.Abstractions.ILojaMudancas mudancas)
    {
        _service = service;
        _mudancas = mudancas;
    }

    [HttpGet("catalogo")]
    public async Task<ActionResult<CatalogoDto>> Catalogo(CancellationToken ct) => Ok(await _service.CatalogoAsync(ct));

    /// <summary>
    /// Sprint 674: o gerador do site deixa este pedido aberto com a versão que tem. Responde logo se o catálogo já é
    /// outro; senão fica à espera e responde no instante em que algo da loja muda (ou ao fim de <paramref name="segundos"/>).
    /// </summary>
    [HttpGet("aguardar")]
    public async Task<ActionResult<object>> Aguardar([FromQuery] string? versao, [FromQuery] int segundos = 50, CancellationToken ct = default)
    {
        var limite = TimeSpan.FromSeconds(Math.Clamp(segundos, 1, 55));
        var inicio = DateTime.UtcNow;
        while (true)
        {
            var marca = _mudancas.Atual;
            var atual = (await _service.CatalogoAsync(ct)).Versao;
            if (!string.Equals(atual, versao, StringComparison.Ordinal)) return Ok(new { versao = atual, mudou = true });
            var falta = limite - (DateTime.UtcNow - inicio);
            if (falta <= TimeSpan.Zero) return Ok(new { versao = atual, mudou = false });
            await _mudancas.EsperarAsync(marca, falta, ct);
            if (ct.IsCancellationRequested) return Ok(new { versao = atual, mudou = false });
            // Várias gravações seguidas (ex.: produto + fotos): espera um instante para publicar tudo de uma vez.
            if (_mudancas.Atual != marca) await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
