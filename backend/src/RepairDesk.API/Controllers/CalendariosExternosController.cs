using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Calendario;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 619 (Doc 98 F4): calendários de fora (Google/Outlook/iCloud) vistos no Mender — cada utilizador os seus.</summary>
[ApiController]
[Authorize]
[Route("api/calendarios-externos")]
public sealed class CalendariosExternosController : ControllerBase
{
    private readonly ICalendarioExternoService _service;
    public CalendariosExternosController(ICalendarioExternoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CalendarioExternoDto>>> List(CancellationToken ct) => Ok(await _service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CalendarioExternoDto>> Ligar([FromBody] LigarCalendarioRequest req, CancellationToken ct)
        => Ok(await _service.LigarAsync(req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await _service.RemoverAsync(id, ct);
        return NoContent();
    }

    [HttpGet("eventos")]
    public async Task<ActionResult<EventosExternosDto>> Eventos([FromQuery] DateTime de, [FromQuery] DateTime ate, CancellationToken ct)
        => Ok(await _service.EventosAsync(DateTime.SpecifyKind(de.ToUniversalTime(), DateTimeKind.Utc), DateTime.SpecifyKind(ate.ToUniversalTime(), DateTimeKind.Utc), ct));
}
