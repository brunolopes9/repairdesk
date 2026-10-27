using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Moloni;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 567: ligação ao Moloni ON (chave da API encriptada + séries por tipo de venda).</summary>
[ApiController]
[Route("api/moloni")]
[Authorize]
public class MoloniController : ControllerBase
{
    private readonly IMoloniService _service;
    private readonly IMoloniGestaoService _gestao;

    public MoloniController(IMoloniService service, IMoloniGestaoService gestao)
    {
        _service = service;
        _gestao = gestao;
    }

    [HttpGet("estado")]
    public Task<MoloniEstadoDto> Estado(CancellationToken ct) => _service.EstadoAsync(ct);

    /// <summary>Guarda/valida a chave (vazia = mantém a atual) e as séries. Confirma que o módulo de API está ativo.</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    public Task<MoloniEstadoDto> Configurar([FromBody] MoloniConfigurarRequest req, CancellationToken ct) => _service.ConfigurarAsync(req, ct);

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remover(CancellationToken ct)
    {
        await _service.RemoverAsync(ct);
        return NoContent();
    }

    // ---------- Sprint 568: documentos, artigos e clientes do Moloni ON ----------

    /// <summary>Documentos do Moloni (incluindo os emitidos fora do Mender), mais recentes primeiro.</summary>
    [HttpGet("documentos")]
    public Task<MoloniPagina<MoloniDocumentoDto>> Documentos([FromQuery] MoloniTipoDocumento tipo, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => _gestao.DocumentosAsync(tipo, q, page, pageSize, ct);

    [HttpGet("documentos/{tipo}/{documentId:int}.pdf")]
    public async Task<IActionResult> DocumentoPdf(MoloniTipoDocumento tipo, int documentId, CancellationToken ct)
    {
        var (pdf, nome) = await _gestao.DocumentoPdfAsync(tipo, documentId, ct);
        return File(pdf, "application/pdf", nome);
    }

    /// <summary>Registar o pagamento de uma fatura por pagar (emite o recibo).</summary>
    [HttpPost("documentos/Fatura/{documentId:int}/recibo")]
    public async Task<IActionResult> Recibo(int documentId, [FromBody] MoloniReciboDocumentoRequest req, CancellationToken ct)
    {
        await _gestao.ReciboDeFaturaAsync(documentId, req, ct);
        return NoContent();
    }

    [HttpPost("documentos/{tipo}/{documentId:int}/anular")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Anular(MoloniTipoDocumento tipo, int documentId, [FromBody] MoloniAnularRequest req, CancellationToken ct)
    {
        await _gestao.AnularDocumentoAsync(tipo, documentId, req.Motivo, ct);
        return NoContent();
    }

    [HttpGet("artigos")]
    public Task<MoloniPagina<MoloniProdutoDto>> Artigos([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => _gestao.ProdutosAsync(q, page, pageSize, ct);

    [HttpGet("referencias")]
    public Task<MoloniReferenciasDto> Referencias(CancellationToken ct) => _gestao.ReferenciasAsync(ct);

    [HttpPost("artigos")]
    [Authorize(Roles = "Admin")]
    public Task<MoloniProdutoDto> CriarArtigo([FromBody] MoloniProdutoRequest req, CancellationToken ct) => _gestao.CriarProdutoAsync(req, ct);

    [HttpPut("artigos/{productId:int}")]
    [Authorize(Roles = "Admin")]
    public Task<MoloniProdutoDto> AtualizarArtigo(int productId, [FromBody] MoloniProdutoRequest req, CancellationToken ct) => _gestao.AtualizarProdutoAsync(productId, req, ct);

    [HttpDelete("artigos/{productId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApagarArtigo(int productId, CancellationToken ct)
    {
        await _gestao.ApagarProdutoAsync(productId, ct);
        return NoContent();
    }

    /// <summary>Liga (pelo NIF) ou cria no Mender os clientes que já existem no Moloni ON.</summary>
    [HttpPost("clientes/importar")]
    [Authorize(Roles = "Admin")]
    public Task<MoloniImportacaoClientesDto> ImportarClientes(CancellationToken ct) => _gestao.ImportarClientesAsync(ct);

    /// <summary>Liga as vendas já faturadas (ex.: importadas do PDF) às faturas e recibos do Moloni ON, pelo nº.</summary>
    [HttpPost("vendas/ligar")]
    [Authorize(Roles = "Admin")]
    public Task<MoloniLigacaoVendasDto> LigarVendas(CancellationToken ct) => _gestao.LigarVendasAsync(ct);
}
