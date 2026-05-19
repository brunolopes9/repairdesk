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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(from, to, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<VendaDto> Get(Guid id, CancellationToken ct)
        => _service.GetAsync(id, ct);

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

    [HttpPost("{id:guid}/cancelar")]
    public Task<VendaDto> Cancelar(Guid id, CancellationToken ct)
        => _service.CancelarAsync(id, ct);

    /// <summary>Emite Nota de Crédito Moloni para anular a fatura (chama API Moloni).</summary>
    [HttpPost("{id:guid}/anular-fatura")]
    public Task<VendaDto> AnularFatura(Guid id, CancellationToken ct)
        => _service.AnularFaturaAsync(id, ct);

    /// <summary>Limpa só referências locais — para casos onde o operador já anulou manualmente no painel Moloni.</summary>
    [HttpPost("{id:guid}/limpar-fatura-local")]
    public Task<VendaDto> LimparFaturaLocal(Guid id, CancellationToken ct)
        => _service.LimparReferenciaFaturaAsync(id, ct);

    [HttpGet("{id:guid}/recibo.pdf")]
    public async Task<IActionResult> ReciboPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _pdf.ForVendaAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }
}
