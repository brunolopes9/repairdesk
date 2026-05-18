using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.EquipmentFields;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/equipment-field-templates")]
[Authorize]
public class EquipmentFieldTemplatesController : ControllerBase
{
    private readonly IEquipmentFieldService _service;

    public EquipmentFieldTemplatesController(IEquipmentFieldService service) => _service = service;

    [HttpGet("active")]
    public Task<IReadOnlyList<EquipmentFieldTemplateDto>> Active(CancellationToken ct)
        => _service.ListActiveAsync(ct);

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public Task<IReadOnlyList<EquipmentFieldTemplateDto>> List([FromQuery] bool includeInactive = true, CancellationToken ct = default)
        => _service.ListAsync(includeInactive, ct);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public Task<EquipmentFieldTemplateDto> Create([FromBody] CreateEquipmentFieldTemplateRequest req, CancellationToken ct)
        => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<EquipmentFieldTemplateDto> Update(Guid id, [FromBody] UpdateEquipmentFieldTemplateRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpPatch("order")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reorder([FromBody] ReorderEquipmentFieldTemplatesRequest req, CancellationToken ct)
    {
        await _service.ReorderAsync(req, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
