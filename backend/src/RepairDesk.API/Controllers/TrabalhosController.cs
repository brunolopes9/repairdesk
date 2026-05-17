using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
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
    public TrabalhosController(ITrabalhoService service, IOrcamentoPdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    [HttpGet("{id:guid}/orcamento.pdf")]
    public async Task<IActionResult> OrcamentoPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _pdf.ForTrabalhoAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

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
