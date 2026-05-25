using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/despesas")]
[Authorize]
public class DespesasController : ControllerBase
{
    private readonly IDespesaService _service;
    public DespesasController(IDespesaService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<DespesaDto>> Search(
        [FromQuery] string? q,
        [FromQuery] DespesaCategoria? categoria,
        [FromQuery(Name = "categoria_in")] string? categoriaIn,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? trabalhoId,
        [FromQuery] Guid? reparacaoId,
        [FromQuery] bool? isRecorrente,
        [FromQuery(Name = "include_supplier_invoice_imports")] bool includeSupplierInvoiceImports,
        [FromQuery(Name = "exclude_supplier_invoice_imports")] bool excludeSupplierInvoiceImports,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, categoria, ParseCategoriaIn(categoriaIn), includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, trabalhoId, reparacaoId, isRecorrente, page, pageSize, ct);

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

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
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
