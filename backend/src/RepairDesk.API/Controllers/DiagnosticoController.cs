using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Diagnostico;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/diagnostico")]
[Authorize]
public class DiagnosticoController : ControllerBase
{
    private readonly IDiagnosticoService _service;
    public DiagnosticoController(IDiagnosticoService service) => _service = service;

    [HttpGet("templates")]
    public Task<IReadOnlyList<DiagnosticoTemplateDto>> Templates(CancellationToken ct)
        => _service.ListTemplatesAsync(ct);

    [HttpPost("templates")]
    public async Task<ActionResult<DiagnosticoTemplateDto>> CreateTemplate([FromBody] CreateTemplateRequest req, CancellationToken ct)
    {
        var t = await _service.CreateTemplateAsync(req, ct);
        return Ok(t);
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        await _service.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    [HttpGet("reparacao/{reparacaoId:guid}")]
    public async Task<ActionResult<DiagnosticoExecucaoDto?>> Get(Guid reparacaoId, CancellationToken ct)
    {
        var e = await _service.GetByReparacaoAsync(reparacaoId, ct);
        if (e is null) return NotFound();
        return Ok(e);
    }

    [HttpPost("reparacao/{reparacaoId:guid}/start")]
    public Task<DiagnosticoExecucaoDto> Start(Guid reparacaoId, [FromBody] StartExecucaoRequest req, CancellationToken ct)
        => _service.StartAsync(reparacaoId, req, ct);

    [HttpPut("reparacao/{reparacaoId:guid}")]
    public Task<DiagnosticoExecucaoDto> Update(Guid reparacaoId, [FromBody] UpdateExecucaoRequest req, CancellationToken ct)
        => _service.UpdateAsync(reparacaoId, req, ct);

    [HttpDelete("reparacao/{reparacaoId:guid}")]
    public async Task<IActionResult> Delete(Guid reparacaoId, CancellationToken ct)
    {
        await _service.DeleteAsync(reparacaoId, ct);
        return NoContent();
    }
}
