using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Reparacoes;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/reparacoes")]
[Authorize]
public class ReparacoesController : ControllerBase
{
    private readonly IReparacaoService _service;
    private readonly IOrcamentoPdfService _pdf;

    public ReparacoesController(IReparacaoService service, IOrcamentoPdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    [HttpGet]
    public Task<PagedResult<ReparacaoDto>> Search(
        [FromQuery] string? q,
        [FromQuery] RepairStatus? estado,
        [FromQuery] Guid? clienteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, estado, clienteId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<ReparacaoDetalhadaDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ReparacaoDto>> Create([FromBody] CreateReparacaoRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<ReparacaoDto> Update(Guid id, [FromBody] UpdateReparacaoRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpPost("{id:guid}/estado")]
    public Task<ReparacaoDto> ChangeEstado(Guid id, [FromBody] ChangeEstadoRequest req, CancellationToken ct)
        => _service.ChangeEstadoAsync(id, req, ct);

    [HttpGet("{id:guid}/orcamento.pdf")]
    public async Task<IActionResult> OrcamentoPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _pdf.ForReparacaoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Histórico de reparações com mesmo IMEI dentro do tenant.</summary>
    [HttpGet("historico-imei")]
    public Task<ReparacaoHistoricoResponse> HistoricoPorImei(
        [FromQuery] string imei,
        [FromQuery] Guid? excludeId,
        CancellationToken ct)
        => _service.HistoricoPorImeiAsync(imei, excludeId, ct);

    /// <summary>Importa reparações em massa a partir de CSV. Cria clientes em falta.</summary>
    [HttpPost("import")]
    public Task<ImportReparacoesResponse> Import([FromBody] ImportReparacoesRequest req, CancellationToken ct)
        => _service.ImportCsvAsync(req.Csv, ct);

    /// <summary>Exporta todas as reparações do tenant em CSV (UTF-8 BOM, Excel-friendly).</summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(ct);
        var filename = $"reparacoes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    public sealed record ReabrirRequest(string? Notas);

    [HttpPost("{id:guid}/reabrir")]
    public Task<ReparacaoDto> Reabrir(Guid id, [FromBody] ReabrirRequest? req, CancellationToken ct)
        => _service.ReabrirAsync(id, req?.Notas, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
