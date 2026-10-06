using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.EquipmentFields;
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
        [FromQuery] DeviceCategory? categoria,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, estado, clienteId, page, pageSize, ct, categoria);

    [HttpGet("pagas-sem-fatura")]
    public Task<IReadOnlyList<ReparacaoDto>> PagasSemFatura([FromQuery] int limit = 100, CancellationToken ct = default)
        => _service.ListPagasSemFaturaAsync(limit, ct);

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

    /// <summary>Sprint 343: atribui (userId) ou desatribui (null) a reparação a um técnico.</summary>
    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = "Admin")]
    public Task<ReparacaoDto> Assign(Guid id, [FromBody] AssignReparacaoRequest req, CancellationToken ct)
        => _service.AssignAsync(id, req.UserId, ct);

    /// <summary>Sprint 346: substitui as tags atribuídas a esta reparação.</summary>
    [HttpPut("{id:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<TagSummaryDto>>> SetTags(
        Guid id,
        [FromBody] SetReparacaoTagsRequest req,
        [FromServices] IReparacaoTagRepository tagRepo,
        CancellationToken ct)
    {
        await tagRepo.SetTagsForReparacaoAsync(id, req.TagIds ?? Array.Empty<Guid>(), ct);
        var tags = await tagRepo.ListByReparacaoAsync(id, ct);
        return Ok(tags.Select(t => new TagSummaryDto(t.Id, t.Nome, t.CorHex)).ToList());
    }

    [HttpPost("{id:guid}/fields")]
    public Task<IReadOnlyList<EquipmentFieldValueDto>> SetFields(Guid id, [FromBody] SetEquipmentFieldValuesRequest req, CancellationToken ct)
        => _service.SetFieldsAsync(id, req, ct);

    [HttpGet("{id:guid}/orcamento.pdf")]
    public async Task<IActionResult> OrcamentoPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _pdf.ForReparacaoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Sprint 347 (Doc 83 Pillar 4): PDF de etiqueta 62×29mm com QR para imprimir em térmica Brother QL.</summary>
    [HttpGet("{id:guid}/label.pdf")]
    public async Task<IActionResult> LabelPdf(Guid id, [FromServices] ILabelPdfService labels, CancellationToken ct)
    {
        var (pdf, filename) = await labels.ForReparacaoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Sprint 450 (Doc 91 ponto 4): PDF "Comprovativo de entrada" para o cliente assinar quando deixa o equipamento.</summary>
    [HttpGet("{id:guid}/entrada.pdf")]
    public async Task<IActionResult> EntradaPdf(Guid id, [FromServices] IEntradaPdfService entrada, CancellationToken ct)
    {
        var (pdf, filename) = await entrada.ForReparacaoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Sprint 451 (Doc 91 ponto 4 — par de S450): PDF "Recibo de entrega" quando o cliente vem buscar o equipamento.</summary>
    [HttpGet("{id:guid}/entrega.pdf")]
    public async Task<IActionResult> EntregaPdf(Guid id, [FromServices] IEntregaPdfService entrega, CancellationToken ct)
    {
        var (pdf, filename) = await entrega.ForReparacaoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Sprint 551 (Doc 80/93): guarda a assinatura do cliente (canvas → PNG) na entrada ou entrega. Recapturar substitui.</summary>
    [HttpPost("{id:guid}/assinaturas")]
    public Task<AssinaturaDto> SaveAssinatura(
        Guid id, [FromBody] SaveAssinaturaRequest req, [FromServices] IAssinaturaService assinaturas, CancellationToken ct)
        => assinaturas.SaveAsync(id, req.Tipo, req.DataUrl, ct);

    /// <summary>Sprint 551: estado das assinaturas da reparação (tipo + quando, sem bytes).</summary>
    [HttpGet("{id:guid}/assinaturas")]
    public Task<IReadOnlyList<AssinaturaDto>> ListAssinaturas(
        Guid id, [FromServices] IAssinaturaService assinaturas, CancellationToken ct)
        => assinaturas.ListAsync(id, ct);

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
    /// <summary>Sprint 551: Tipo = "entrada" | "entrega"; DataUrl = data:image/png;base64,...</summary>
    public sealed record SaveAssinaturaRequest(string Tipo, string DataUrl);

    [HttpPost("{id:guid}/reabrir")]
    public Task<ReparacaoDto> Reabrir(Guid id, [FromBody] ReabrirRequest? req, CancellationToken ct)
        => _service.ReabrirAsync(id, req?.Notas, ct);

    // Sprint 237 H1.1: apagar reparação é destrutivo (soft-delete mas remove do histórico
    // visível e dos cálculos de KPI). Só Admin.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
