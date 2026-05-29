using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.InternalTasks;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 422 (Doc 90 Tier 2 #7): tarefas internas. Qualquer staff autenticado
/// pode criar/atualizar/concluir tarefas (não é restritivo como inventário).
/// </summary>
[ApiController]
[Authorize]
[Route("api/internal-tasks")]
public sealed class InternalTasksController : ControllerBase
{
    private readonly IInternalTaskService _service;

    public InternalTasksController(IInternalTaskService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InternalTaskDto>>> List(
        [FromQuery] InternalTaskStatus? status,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] Guid? reparacaoId,
        CancellationToken ct)
        => Ok(await _service.ListAsync(status, assignedToUserId, reparacaoId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InternalTaskDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<InternalTaskDto>> Create([FromBody] CreateInternalTaskRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InternalTaskDto>> Update(Guid id, [FromBody] UpdateInternalTaskRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<InternalTaskDto>> ChangeStatus(Guid id, [FromBody] ChangeInternalTaskStatusRequest req, CancellationToken ct)
        => Ok(await _service.ChangeStatusAsync(id, req.Status, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
