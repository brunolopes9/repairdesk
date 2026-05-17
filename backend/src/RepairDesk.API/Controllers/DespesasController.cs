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
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? trabalhoId,
        [FromQuery] Guid? reparacaoId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, categoria, from, to, trabalhoId, reparacaoId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<DespesaDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<DespesaDto>> Create([FromBody] CreateDespesaRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<DespesaDto> Update(Guid id, [FromBody] UpdateDespesaRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
