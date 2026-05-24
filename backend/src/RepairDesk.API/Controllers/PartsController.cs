using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Files;
using RepairDesk.Services.Parts;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/parts")]
[Authorize]
public class PartsController : ControllerBase
{
    private readonly IPartService _service;
    private readonly IFileValidator _fileValidator;

    public PartsController(IPartService service, IFileValidator fileValidator)
    {
        _service = service;
        _fileValidator = fileValidator;
    }

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

    // Sprint 243 Fase A: ajuste manual de stock pode esconder shrinkage e desviar
    // KPI. Import CSV insere muitas linhas — admin-only. Doc 72 §2 A.4.
    [HttpPost("{id:guid}/movimento")]
    [Authorize(Roles = "Admin")]
    public Task<PartMovimentoDto> AddMovimento(Guid id, [FromBody] CreatePartMovimentoRequest req, CancellationToken ct)
        => _service.AddMovimentoAsync(id, req, ct);

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
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

        // Sprint 247 (Doc 73 Fase B): substitui o OR ContentType/extension fraco por
        // validação por magic bytes — PdfPig vai abrir o stream confiando que é PDF real.
        await using var raw = file.OpenReadStream();
        var validated = await _fileValidator.ValidateAsync(raw, file.ContentType, FileKind.Pdf, ct);

        try
        {
            using var pdfStream = new MemoryStream(validated.Buffer);
            var result = RepairDesk.Services.Documents.PdfTextExtractor.Extract(pdfStream, file.FileName);
            // Sprint 124: anexa sugestões parseadas para o frontend popular o form.
            var suggestions = RepairDesk.Services.Documents.SupplierPdfParser.Parse(result.Text);
            return Ok(result with { Suggestions = suggestions });
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = $"PDF inválido ou corrompido: {ex.Message}" });
        }
    }

    /// <summary>
    /// Sprint 214: lista PartMovimentos cuja Reparação está soft-deleted (IsDeleted=true).
    /// São dados sujos — Sprint 208 já os exclui de cálculos mas ficam no histórico.
    /// Bruno usa antes de chamar purge para confirmar o que vai ser apagado.
    /// </summary>
    [HttpGet("admin/orphan-movimentos")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListOrphanMovimentos(
        [FromServices] RepairDesk.DAL.Persistence.AppDbContext db,
        CancellationToken ct)
    {
        var orphans = await db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.ReparacaoId != null
                && db.Reparacoes.IgnoreQueryFilters().Any(r => r.Id == m.ReparacaoId && r.IsDeleted))
            .Include(m => m.Part)
            .Select(m => new
            {
                m.Id,
                m.Quantidade,
                m.Motivo,
                m.CreatedAt,
                m.ReparacaoId,
                m.Notas,
                PartSku = m.Part != null ? m.Part.Sku : null,
                PartNome = m.Part != null ? m.Part.Nome : null,
            })
            .OrderByDescending(m => m.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return Ok(new { count = orphans.Count, items = orphans });
    }

    /// <summary>
    /// Sprint 214: hard-delete PartMovimentos cuja Reparação está soft-deleted.
    /// Apenas Admin. Audita o número apagado.
    /// </summary>
    [HttpPost("admin/orphan-movimentos/purge")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PurgeOrphanMovimentos(
        [FromServices] RepairDesk.DAL.Persistence.AppDbContext db,
        [FromServices] RepairDesk.Core.Abstractions.IAuditLogger audit,
        CancellationToken ct)
    {
        var orphans = await db.PartMovimentos
            .Where(m => m.ReparacaoId != null
                && db.Reparacoes.IgnoreQueryFilters().Any(r => r.Id == m.ReparacaoId && r.IsDeleted))
            .ToListAsync(ct);

        if (orphans.Count == 0) return Ok(new { purged = 0 });

        db.PartMovimentos.RemoveRange(orphans);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(
            RepairDesk.Core.Enums.AuditAction.Delete,
            nameof(RepairDesk.Core.Entities.PartMovimento),
            null,
            new { purged = orphans.Count, source = "admin_orphan_cleanup" },
            ct: ct);

        return Ok(new { purged = orphans.Count });
    }
}
