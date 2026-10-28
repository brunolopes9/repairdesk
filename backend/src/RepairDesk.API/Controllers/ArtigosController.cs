using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Vendas;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 566: artigos da loja (SKU + código de barras), etiquetas e venda ao balcão.</summary>
[ApiController]
[Route("api/artigos")]
[Authorize]
public class ArtigosController : ControllerBase
{
    private readonly IArtigoService _service;
    public ArtigosController(IArtigoService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<ArtigoDto>> List([FromQuery] string? q, [FromQuery] bool incluirInativos, CancellationToken ct)
        => _service.ListAsync(q, incluirInativos, ct);

    [HttpGet("{id:guid}")]
    public Task<ArtigoDetalheDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    /// <summary>O que a pistola leu: código de barras, código do fabricante ou SKU.</summary>
    [HttpGet("codigo/{codigo}")]
    public Task<ArtigoDto> PorCodigo(string codigo, CancellationToken ct) => _service.PorCodigoAsync(codigo, ct);

    /// <summary>Sprint 569: lotes em stock sem artigo, agrupados pela descrição.</summary>
    [HttpGet("sugestoes")]
    public Task<IReadOnlyList<ArtigoSugestaoDto>> Sugestoes(CancellationToken ct) => _service.SugestoesAsync(ct);

    /// <summary>Sprint 570: categorias com preço por defeito (ex.: Películas a 15 €).</summary>
    [HttpGet("categorias")]
    public Task<IReadOnlyList<CategoriaArtigoDto>> Categorias(CancellationToken ct) => _service.CategoriasAsync(ct);

    [HttpPut("categorias")]
    [Authorize(Roles = "Admin")]
    public Task<CategoriaArtigoDto> GuardarCategoria([FromBody] CategoriaArtigoRequest req, CancellationToken ct) => _service.GuardarCategoriaAsync(req, ct);

    [HttpPost("em-lote")]
    public Task<IReadOnlyList<ArtigoDto>> CriarEmLote([FromBody] ArtigosEmLoteRequest req, CancellationToken ct) => _service.CriarEmLoteAsync(req, ct);

    [HttpPost]
    public Task<ArtigoDetalheDto> Create([FromBody] ArtigoWriteRequest req, CancellationToken ct) => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<ArtigoDetalheDto> Update(Guid id, [FromBody] ArtigoWriteRequest req, CancellationToken ct) => _service.UpdateAsync(id, req, ct);

    /// <summary>Sprint 581: junta artigos repetidos neste — os lotes passam e as etiquetas antigas continuam a ler.</summary>
    [HttpPost("{id:guid}/fundir")]
    [Authorize(Roles = "Admin")]
    public Task<ArtigoDetalheDto> Fundir(Guid id, [FromBody] FundirArtigosRequest req, CancellationToken ct) => _service.FundirAsync(id, req, ct);

    /// <summary>Sprint 581: desfaz uma junção de artigos (o artigo junto volta com os seus lotes e códigos).</summary>
    [HttpPost("fusoes/{fusaoId:guid}/desfazer")]
    [Authorize(Roles = "Admin")]
    public Task<ArtigoDetalheDto> DesfazerFusao(Guid fusaoId, CancellationToken ct) => _service.DesfazerFusaoAsync(fusaoId, ct);

    /// <summary>Sprint 581: lotes que são peças de reparação (ecrãs, baterias…) — saem da lista "por ligar".</summary>
    [HttpPut("lotes/peca-reparacao")]
    public Task<int> PecaReparacao([FromBody] PecaReparacaoRequest req, CancellationToken ct) => _service.MarcarPecaReparacaoAsync(req, ct);

    [HttpDelete("{id:guid}/lotes/{loteId:guid}")]
    public Task<ArtigoDetalheDto> DesligarLote(Guid id, Guid loteId, CancellationToken ct) => _service.DesligarLoteAsync(id, loteId, ct);

    /// <summary>PDF com uma etiqueta por página, do tamanho do rolo (Marklife P50: 50×30 mm).</summary>
    [HttpPost("etiquetas.pdf")]
    public async Task<IActionResult> Etiquetas([FromBody] EtiquetasRequest req, [FromServices] IEtiquetaPdfService etiquetas, CancellationToken ct)
        => File(await etiquetas.GerarAsync(req, ct), "application/pdf", "etiquetas.pdf");

    /// <summary>Sprint 582: como sairia a fatura desta venda (texto das linhas, preços) — sem vender nem emitir.</summary>
    [HttpPost("balcao/previsualizar")]
    public Task<IReadOnlyList<BalcaoLinhaPreviaDto>> Previsualizar([FromBody] BalcaoVendaRequest req, CancellationToken ct) => _service.PrevisualizarAsync(req, ct);

    /// <summary>Venda ao balcão (pistola): tira dos lotes mais antigos e cria a venda entregue.</summary>
    [HttpPost("balcao/vender")]
    public Task<VendaDto> Vender([FromBody] BalcaoVendaRequest req, CancellationToken ct) => _service.VenderAsync(req, ct);
}
