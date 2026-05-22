using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Parts;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/parts")]
[Authorize]
public class PartsController : ControllerBase
{
    private readonly IPartService _service;

    public PartsController(IPartService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<PartDto>> Search(
        [FromQuery] string? q,
        [FromQuery] PartCategoria? categoria,
        [FromQuery] string? marca,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(q, categoria, marca, lowStockOnly, page, pageSize, ct);

    [HttpGet("low-stock")]
    public Task<IReadOnlyList<PartDto>> LowStock(CancellationToken ct)
        => _service.LowStockAsync(ct);

    /// <summary>Sprint 186: previsão reabastecer — Parts em risco de ruptura nos próximos N dias.</summary>
    [HttpGet("reabastecer-sugestoes")]
    public Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoes([FromQuery] int days = 30, CancellationToken ct = default)
        => _service.ReabastecerSugestoesAsync(days, ct);

    [HttpGet("marcas")]
    public Task<IReadOnlyList<string>> Marcas(CancellationToken ct)
        => _service.MarcasAsync(ct);

    [HttpGet("movimentos")]
    public Task<IReadOnlyList<PartMovimentoDto>> Movimentos(
        [FromQuery] Guid? partId,
        [FromQuery] Guid? reparacaoId,
        CancellationToken ct)
        => _service.MovimentosAsync(partId, reparacaoId, ct);

    [HttpGet("{id:guid}")]
    public Task<PartDto> Get(Guid id, CancellationToken ct)
        => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PartDto>> Create([FromBody] CreatePartRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<PartDto> Update(Guid id, [FromBody] UpdatePartRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/movimentos")]
    public Task<IReadOnlyList<PartMovimentoDto>> MovimentosByPart(Guid id, CancellationToken ct)
        => _service.MovimentosAsync(id, null, ct);

    [HttpPost("{id:guid}/movimento")]
    public Task<PartMovimentoDto> AddMovimento(Guid id, [FromBody] CreatePartMovimentoRequest req, CancellationToken ct)
        => _service.AddMovimentoAsync(id, req, ct);

    [HttpPost("import")]
    public Task<ImportPartsResponse> Import([FromBody] ImportPartsRequest req, CancellationToken ct)
        => _service.ImportCsvAsync(req.Csv, ct);

    /// <summary>
    /// Sprint 119: extrai texto de um PDF de encomenda/fatura recebida do fornecedor
    /// (Tudo4Mobile, Molano, etc). Devolve texto bruto para preencher manualmente um
    /// form de criação de peça. Sem parsing AI — apenas extracção. Limite 10 MB / 30 páginas.
    /// </summary>
    [HttpPost("extract-pdf")]
    [RequestSizeLimit(RepairDesk.Services.Documents.PdfTextExtractor.MaxBytes)]
    public async Task<ActionResult<RepairDesk.Services.Documents.PdfExtractionResult>> ExtractPdf(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Ficheiro não fornecido." });
        if (file.Length > RepairDesk.Services.Documents.PdfTextExtractor.MaxBytes)
            return BadRequest(new { detail = $"Ficheiro demasiado grande (máx {RepairDesk.Services.Documents.PdfTextExtractor.MaxBytes / 1024 / 1024} MB)." });
        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = "Apenas PDFs são aceites." });

        await using var stream = file.OpenReadStream();
        try
        {
            var result = RepairDesk.Services.Documents.PdfTextExtractor.Extract(stream, file.FileName);
            // Sprint 124: anexa sugestões parseadas para o frontend popular o form.
            var suggestions = RepairDesk.Services.Documents.SupplierPdfParser.Parse(result.Text);
            return Ok(result with { Suggestions = suggestions });
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = $"PDF inválido ou corrompido: {ex.Message}" });
        }
    }
}
