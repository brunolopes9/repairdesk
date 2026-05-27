using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Appointments;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 371: agendamentos (booking) — qualquer empregado autenticado gere a agenda.
/// </summary>
[ApiController]
[Authorize]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;

    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }

    /// <summary>Lista agendamentos num intervalo [from, to). Default: próximos 30 dias.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> List(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status, CancellationToken ct)
    {
        var fromUtc = (from ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc = (to ?? fromUtc.AddDays(30)).ToUniversalTime();
        AppointmentStatus? st = Enum.TryParse<AppointmentStatus>(status, true, out var s) ? s : null;
        return Ok(await _service.ListAsync(fromUtc, toUtc, st, ct));
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
        => Ok(await _service.CreateAsync(req, AppointmentSource.Balcao, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(Guid id, [FromBody] UpdateAppointmentStatusRequest req, CancellationToken ct)
        => Ok(await _service.UpdateStatusAsync(id, req.Status, ct));

    [HttpPatch("{id:guid}/reschedule")]
    public async Task<ActionResult<AppointmentDto>> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest req, CancellationToken ct)
        => Ok(await _service.RescheduleAsync(id, req.ScheduledAt, req.DurationMin, ct));
}
