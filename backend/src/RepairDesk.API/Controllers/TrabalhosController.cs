using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Trabalhos;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/trabalhos")]
[Authorize]
public class TrabalhosController : ControllerBase
{
    private readonly ITrabalhoService _service;
    private readonly IOrcamentoPdfService _pdf;
    private readonly IBillingProvider _billing;

    public TrabalhosController(ITrabalhoService service, IOrcamentoPdfService pdf, IBillingProvider billing)
    {
        _service = service;
        _pdf = pdf;
        _billing = billing;
    }

    [HttpGet("{id:guid}/orcamento.pdf")]
    public async Task<IActionResult> OrcamentoPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _pdf.ForTrabalhoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    [HttpPost("{id:guid}/emitir-fatura")]
    public Task<InvoiceDto> EmitirFatura(Guid id, [FromBody] EmitInvoiceRequest? req, CancellationToken ct)
        => _billing.EmitTrabalhoInvoiceAsync(id, req?.VatPercent, req?.PaymentMethod, ct);

    [HttpPost("{id:guid}/emitir-orcamento-moloni")]
    public Task<TrabalhoDto> EmitirOrcamentoMoloni(Guid id, CancellationToken ct)
        => _service.EmitirOrcamentoMoloniAsync(id, ct);

    [HttpPost("{id:guid}/converter-orcamento-fatura")]
    public Task<TrabalhoDto> ConverterOrcamentoEmFatura(Guid id, CancellationToken ct)
        => _service.ConverterOrcamentoEmFaturaAsync(id, ct);

    /// <summary>Emite Nota de Credito Moloni + limpa referencias locais.</summary>
    [HttpPost("{id:guid}/anular-fatura")]
    public Task<TrabalhoDto> AnularFatura(Guid id, CancellationToken ct)
        => _service.AnularFaturaAsync(id, ct);

    [HttpGet("pagas-sem-fatura")]
    public Task<IReadOnlyList<TrabalhoDto>> PagasSemFatura([FromQuery] int limit = 100, CancellationToken ct = default)
        => _service.ListPagasSemFaturaAsync(limit, ct);

    /// <summary>Emite fatura para vários trabalhos pagos em batch.</summary>
    [HttpPost("bulk-emit-faturas")]
    public async Task<IReadOnlyList<BulkEmitResult>> BulkEmitFaturas([FromBody] BulkEmitRequest req, CancellationToken ct)
    {
        if (req.Ids is null || req.Ids.Count == 0)
            return Array.Empty<BulkEmitResult>();

        var results = new List<BulkEmitResult>(req.Ids.Count);
        foreach (var id in req.Ids)
        {
            try
            {
                var invoice = await _billing.EmitTrabalhoInvoiceAsync(id, null, null, ct);
                results.Add(new BulkEmitResult(id, true, invoice.Number, null));
            }
            catch (Exception ex)
            {
                results.Add(new BulkEmitResult(id, false, null, ex.Message));
            }
        }
        return results;
    }

    public sealed record BulkEmitRequest(IReadOnlyList<Guid> Ids);
    public sealed record BulkEmitResult(Guid Id, bool Success, string? InvoiceNumber, string? ErrorMessage);

    [HttpPost("{id:guid}/reabrir")]
    public Task<TrabalhoDto> Reabrir(Guid id, CancellationToken ct) => _service.ReabrirAsync(id, ct);

    [HttpGet]
    public Task<PagedResult<TrabalhoDto>> Search(
        [FromQuery] string? q,
        [FromQuery] TrabalhoStatus? status,
        [FromQuery] JobCategory? categoria,
        [FromQuery] Guid? clienteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, status, categoria, clienteId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<TrabalhoDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<TrabalhoDto>> Create([FromBody] CreateTrabalhoRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<TrabalhoDto> Update(Guid id, [FromBody] UpdateTrabalhoRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
