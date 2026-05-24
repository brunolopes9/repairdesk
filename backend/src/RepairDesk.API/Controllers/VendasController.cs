using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Cash;
using RepairDesk.Core.Enums;
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
    private readonly ICashService _cash;
    private readonly ILogger<VendasController> _logger;

    public VendasController(IVendaService service, IVendaPdfService pdf, ICashService cash, ILogger<VendasController> logger)
    {
        _service = service;
        _pdf = pdf;
        _cash = cash;
        _logger = logger;
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

    // Sprint 301 (Doc 80 Pillar A.1): após marcar paga, regista automaticamente
    // CashMovement no fecho do dia. Falha no cash NÃO reverte a venda — o pagamento
    // está confirmado e perder-se o lançamento de caixa é menos grave que duplicar
    // o pagamento. Log estruturado para reconciliação manual se ocorrer.
    [HttpPost("{id:guid}/marcar-paga")]
    public async Task<EmitVendaFaturaResponse> MarcarPaga(Guid id, [FromBody] MarcarVendaPagaRequest req, CancellationToken ct)
    {
        var result = await _service.MarcarPagaAsync(id, req, ct);

        try
        {
            var venda = await _service.GetAsync(id, ct);
            var descricao = $"Venda #{venda.Numero}"
                + (venda.Cliente is { } c ? $" — {c.Nome}" : "")
                + (req.EmitirFatura && !string.IsNullOrEmpty(venda.InvoiceNumber) ? $" (FT {venda.InvoiceNumber})" : "");

            await _cash.RecordMovementAsync(new RecordMovementRequest(
                Type: CashMovementType.PagamentoCliente,
                PaymentMethod: req.PaymentMethod,
                AmountCents: venda.TotalCents,
                Descricao: descricao,
                VendaId: venda.Id,
                ReparacaoId: null,
                LocationId: null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CashMovementRecordFailed VendaId={VendaId} Method={Method} — venda continua paga, requer reconciliação manual",
                id, req.PaymentMethod);
        }

        return result;
    }

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
