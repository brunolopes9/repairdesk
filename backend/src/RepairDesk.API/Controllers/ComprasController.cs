using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Compras.Import;

namespace RepairDesk.API.Controllers;

/// <summary>Compras a fornecedores = stock por lote (Doc 94 Fase 3).</summary>
[ApiController]
[Route("api/compras")]
[Authorize]
public class ComprasController : ControllerBase
{
    private const long MaxExcelBytes = 10 * 1024 * 1024;

    private readonly ICompraService _service;
    private readonly IComprasExcelImporter _importer;
    private readonly ICompraFaturaService _faturas;

    public ComprasController(ICompraService service, IComprasExcelImporter importer, ICompraFaturaService faturas)
    {
        _service = service;
        _importer = importer;
        _faturas = faturas;
    }

    [HttpGet]
    public Task<PagedResult<CompraDocumentoDto>> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] bool faturaEmFalta = false,
        [FromQuery] DateTime? de = null,
        [FromQuery] DateTime? ate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(new CompraFiltro(q, fornecedorId, faturaEmFalta,
            de is { } d ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null,
            ate is { } a ? DateTime.SpecifyKind(a.Date.AddDays(1), DateTimeKind.Utc) : null), page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<CompraDocumentoDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<CompraDocumentoDto>> Create([FromBody] CompraDocumentoWriteRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Aprova uma fatura recebida (lida por IA) como compra — cria os lotes e fecha a importação.</summary>
    [HttpPost("de-fatura/{importId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CompraDocumentoDto>> CreateFromImport(Guid importId, [FromBody] CompraDocumentoWriteRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateFromImportAsync(importId, req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Sprint 560: que compra(s) podem corresponder a uma fatura recebida, com a comparação.</summary>
    [HttpGet("faturas-recebidas/{importId:guid}/correspondencia")]
    public Task<CorrespondenciaFaturaDto> Correspondencia(Guid importId, CancellationToken ct)
        => _faturas.CorrespondenciaAsync(importId, ct);

    /// <summary>Sprint 560: liga a fatura recebida a uma compra existente (nº, data, totais e PDF da fatura).</summary>
    [HttpPost("{id:guid}/associar-fatura/{importId:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<CompraDocumentoDto> AssociarFatura(Guid id, Guid importId, CancellationToken ct)
        => _faturas.AssociarAsync(id, importId, ct);

    /// <summary>Sprint 560: associa sozinho todas as faturas pendentes que batem certo com uma única compra.</summary>
    [HttpPost("faturas-recebidas/associar-automaticamente")]
    [Authorize(Roles = "Admin")]
    public Task<AssociacaoAutomaticaDto> AssociarAutomaticamente(CancellationToken ct)
        => _faturas.AssociarAutomaticamenteAsync(ct);

    [HttpPut("{id:guid}")]
    public Task<CompraDocumentoDto> Update(Guid id, [FromBody] CompraDocumentoWriteRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Lotes com stock &gt; 0 (SPEC §4.5).</summary>
    [HttpGet("inventario")]
    public Task<IReadOnlyList<InventarioLinhaDto>> Inventario(CancellationToken ct) => _service.InventarioAsync(ct);

    /// <summary>"Já vendido" vs "em stock", IVA das compras e resumo por fornecedor (SPEC §4).</summary>
    [HttpGet("resumo")]
    public Task<ResumoComprasDto> Resumo(CancellationToken ct) => _service.ResumoAsync(ct);

    /// <summary>Calculadora de uma peça (SPEC §4.6) — sem gravar nada.</summary>
    [HttpPost("simulador")]
    public SimuladorResponse Simulador([FromBody] SimuladorRequest req) => _service.Simular(req);

    /// <summary>Importa o Excel de compras (folhas Fornecedores/Faturas/Compras). Idempotente.</summary>
    [HttpPost("importar-excel")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxExcelBytes)]
    public async Task<ActionResult<ImportComprasResultado>> ImportarExcel(IFormFile ficheiro, CancellationToken ct)
    {
        if (ficheiro is null || ficheiro.Length == 0)
            return BadRequest(new { code = "ficheiro_vazio", message = "Escolhe o ficheiro .xlsx." });
        if (ficheiro.Length > MaxExcelBytes)
            return BadRequest(new { code = "ficheiro_grande", message = "Máximo 10 MB." });

        await using var stream = ficheiro.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return await _importer.ImportAsync(buffer, ct);
    }
}
