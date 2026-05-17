using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
