using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Files;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/despesas")]
[Authorize]
public class DespesasController : ControllerBase
{
    private readonly IDespesaService _service;
    public DespesasController(IDespesaService service) => _service = service;

    /// <summary>Sprint 574: totais de todas as despesas do filtro (a lista é paginada; os totais não).</summary>
    [HttpGet("resumo")]
    public Task<DespesasResumoDto> Resumo(
        [FromQuery] string? q,
        [FromQuery] DespesaCategoria? categoria,
        [FromQuery(Name = "categoria_in")] string? categoriaIn,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? isRecorrente,
        [FromQuery(Name = "include_supplier_invoice_imports")] bool includeSupplierInvoiceImports,
        [FromQuery(Name = "exclude_supplier_invoice_imports")] bool excludeSupplierInvoiceImports,
        [FromQuery] DespesaAmbito? ambito = null,
        CancellationToken ct = default)
        => _service.ResumoAsync(q, categoria, ParseCategoriaIn(categoriaIn), includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente, ct, Ambito(ambito));

    [HttpGet]
    public Task<PagedResult<DespesaDto>> Search(
        [FromQuery] string? q,
        [FromQuery] DespesaCategoria? categoria,
        [FromQuery(Name = "categoria_in")] string? categoriaIn,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? isRecorrente,
        [FromQuery(Name = "include_supplier_invoice_imports")] bool includeSupplierInvoiceImports,
        [FromQuery(Name = "exclude_supplier_invoice_imports")] bool excludeSupplierInvoiceImports,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DespesaAmbito? ambito = null,
        CancellationToken ct = default)
        => _service.SearchAsync(q, categoria, ParseCategoriaIn(categoriaIn), includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente, page, pageSize, ct, Ambito(ambito));

    /// <summary>Sprint 633: as despesas pessoais e obrigações do dono só o Admin vê; os outros só veem as do negócio.</summary>
    private DespesaAmbito? Ambito(DespesaAmbito? pedido) => User.IsInRole("Admin") ? pedido : DespesaAmbito.Negocio;

    [HttpGet("{id:guid}")]
    public Task<DespesaDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    // Sprint 243 Fase A: despesas alimentam IVA dedutível (Sprint 159) e Lucro/OpEx no
    // Relatório Negócio. Criar/editar/apagar é admin-only para evitar manipulação
    // fiscal por funcionário sem autorização. Doc 72 §2 A.3.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DespesaDto>> Create([FromBody] CreateDespesaRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<DespesaDto> Update(Guid id, [FromBody] UpdateDespesaRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    /// <summary>Sprint 646: classificação em lote (só gestão / pertence a / afetação) pelo nº visível.</summary>
    [HttpPost("classificar")]
    [Authorize(Roles = "Admin")]
    public Task<ClassificarDespesasResultado> Classificar([FromBody] IReadOnlyList<ClassificarDespesa> itens, CancellationToken ct)
        => _service.ClassificarAsync(itens, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sprint 586: anexa o PDF (ou a foto) da fatura a uma despesa criada à mão, para a abrir e imprimir como nas
    /// compras. Sem leitura por IA — os dados ficam os da despesa.
    /// </summary>
    [HttpPost("{id:guid}/pdf")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<DespesaDto>> AnexarPdf(
        Guid id, [FromForm] IFormFile file,
        [FromServices] ISupplierInvoiceImportService imports, [FromServices] IFileValidator validator, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "no_file", detail = "Escolhe o PDF da fatura." });
        await using var stream = file.OpenReadStream();
        var kind = file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? FileKind.Pdf : FileKind.Image;
        var validated = await validator.ValidateAsync(stream, file.ContentType, kind, ct);
        await imports.AnexarADespesaAsync(id, validated.Buffer, file.FileName, ct);
        return await _service.GetAsync(id, ct);
    }

    private static IReadOnlyCollection<DespesaCategoria>? ParseCategoriaIn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var values = new List<DespesaCategoria>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var numeric) && Enum.IsDefined(typeof(DespesaCategoria), numeric))
            {
                values.Add((DespesaCategoria)numeric);
                continue;
            }

            if (Enum.TryParse<DespesaCategoria>(part, ignoreCase: true, out var named) &&
                Enum.IsDefined(typeof(DespesaCategoria), named))
            {
                values.Add(named);
            }
        }

        return values.Count == 0 ? null : values.Distinct().ToArray();
    }
}
