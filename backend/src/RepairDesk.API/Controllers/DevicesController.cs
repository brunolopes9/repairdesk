using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Devices;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 461 (Doc 90 Tier 2 #6 — Asset registry): CRUD de equipamentos persistentes do cliente.
/// Eixo cliente (não reparação) — vive entre reparações, permite histórico cross-rep.
/// </summary>
[ApiController]
[Authorize]
[Route("api/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly IDeviceService _service;

    public DevicesController(IDeviceService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<DeviceDto>> List(
        [FromQuery] Guid clienteId,
        [FromQuery] bool incluirArquivados = false,
        CancellationToken ct = default)
        => _service.ListByClienteAsync(clienteId, incluirArquivados, ct);

    [HttpGet("{id:guid}")]
    public Task<DeviceDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create([FromBody] CreateDeviceRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<DeviceDto> Update(Guid id, [FromBody] UpdateDeviceRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
