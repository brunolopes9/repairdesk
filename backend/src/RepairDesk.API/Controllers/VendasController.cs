using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Vendas;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/vendas")]
[Authorize]
public class VendasController : ControllerBase
{
    private readonly IVendaService _service;
    private readonly IVendaPdfService _pdf;

    public VendasController(IVendaService service, IVendaPdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    [HttpGet]
    public Task<PagedResult<VendaDto>> Search(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? clienteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(from, to, clienteId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<VendaDto> Get(Guid id, CancellationToken ct)
        => _service.GetAsync(id, ct);

    /// <summary>Retorna a primeira venda que ja vendeu este IMEI (warning anti-duplicação). 404 se nunca vendido.</summary>
    [HttpGet("imei-lookup/{imei}")]
    public async Task<ActionResult<VendaImeiLookupDto>> ImeiLookup(string imei, CancellationToken ct)
    {
        var hit = await _service.ImeiLookupAsync(imei, ct);
        return hit is null ? NotFound() : Ok(hit);
    }

    /// <summary>Lista de fornecedores já usados pelo tenant — para autocomplete na UI ao criar venda.</summary>
    [HttpGet("fornecedores")]
    public Task<IReadOnlyList<string>> Fornecedores(CancellationToken ct) => _service.ListFornecedoresAsync(ct);

    /// <summary>
    /// Reparações cujo IMEI bate items desta venda (e foram criadas depois). Para ver se um
    /// equipamento vendido voltou para reparação.
    /// </summary>
    [HttpGet("{id:guid}/reparacoes-relacionadas")]
    public Task<IReadOnlyList<VendaReparacaoRelacionadaDto>> ReparacoesRelacionadas(Guid id, CancellationToken ct)
        => _service.GetReparacoesRelacionadasAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<VendaDto>> Create([FromBody] CreateVendaRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPost("{id:guid}/marcar-paga")]
    public Task<EmitVendaFaturaResponse> MarcarPaga(Guid id, [FromBody] MarcarVendaPagaRequest req, CancellationToken ct)
        => _service.MarcarPagaAsync(id, req, ct);

    [HttpPost("{id:guid}/emitir-fatura")]
    public Task<InvoiceDto> EmitirFatura(Guid id, CancellationToken ct)
        => _service.EmitirFaturaAsync(id, ct);

    // Sprint 237 H1.1: cancelar venda + anular fatura + limpar fatura são operações
    // destrutivas com impacto fiscal (Moloni NC). Só Admin.
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "Admin")]
    public Task<VendaDto> Cancelar(Guid id, CancellationToken ct)
        => _service.CancelarAsync(id, ct);

    /// <summary>Emite Nota de Crédito Moloni para anular a fatura (chama API Moloni).</summary>
    [HttpPost("{id:guid}/anular-fatura")]
    [Authorize(Roles = "Admin")]
    public Task<VendaDto> AnularFatura(Guid id, CancellationToken ct)
        => _service.AnularFaturaAsync(id, ct);

    /// <summary>Limpa só referências locais — para casos onde o operador já anulou manualmente no painel Moloni.</summary>
    [HttpPost("{id:guid}/limpar-fatura-local")]
    [Authorize(Roles = "Admin")]
    public Task<VendaDto> LimparFaturaLocal(Guid id, CancellationToken ct)
        => _service.LimparReferenciaFaturaAsync(id, ct);

    [HttpGet("{id:guid}/recibo.pdf")]
    public async Task<IActionResult> ReciboPdf(Guid id, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (pdf, filename) = await _pdf.ForVendaAsync(id, baseUrl, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Exporta vendas em CSV (Excel-friendly, UTF-8 com BOM). Filtrado por intervalo de datas.</summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(from, to, ct);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return File(bytes, "text/csv; charset=utf-8", $"vendas_{stamp}.csv");
    }
}
