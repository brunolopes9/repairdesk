using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Avencas;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 546 (Doc 93 #1): avenças — faturação recorrente. Writes e emissão são Admin-only
/// (criam documentos fiscais reais via Moloni), como o resto da faturação.
/// </summary>
[ApiController]
[Route("api/avencas")]
[Authorize]
public sealed class AvencasController : ControllerBase
{
    private readonly IAvencaService _service;
    public AvencasController(IAvencaService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<AvencaDto>> List([FromQuery] Guid? clienteId, CancellationToken ct)
        => _service.ListAsync(clienteId, ct);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public Task<AvencaDto> Create([FromBody] SaveAvencaRequest req, CancellationToken ct)
        => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<AvencaDto> Update(Guid id, [FromBody] SaveAvencaRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // O "1 clique" do modo conservador: cria o Trabalho do período + emite a FT Moloni.
    [HttpPost("{id:guid}/emitir")]
    [Authorize(Roles = "Admin")]
    public Task<AvencaEmissaoResult> Emitir(Guid id, CancellationToken ct)
        => _service.EmitirAsync(id, ct);
}
