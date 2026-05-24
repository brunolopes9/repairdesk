using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Cash;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): POS PT — controlo de caixa.
///
/// Endpoints destrutivos (close-day) só Admin — fechar caixa errado tem impacto
/// fiscal. RecordMovement aceita any authenticated user (cashier típico).
/// </summary>
[ApiController]
[Route("api/cash")]
[Authorize]
public sealed class CashController : ControllerBase
{
    private readonly ICashService _service;
    public CashController(ICashService service) => _service = service;

    [HttpGet("today")]
    public Task<DailyClosingDto?> Today([FromQuery] Guid? locationId, CancellationToken ct)
        => _service.GetTodayAsync(locationId, ct);

    [HttpGet("by-date/{date}")]
    public Task<DailyClosingDto?> ByDate(DateOnly date, [FromQuery] Guid? locationId, CancellationToken ct)
        => _service.GetByDateAsync(date, locationId, ct);

    [HttpGet("recent")]
    public Task<IReadOnlyList<DailyClosingDto>> Recent([FromQuery] int take = 30, [FromQuery] Guid? locationId = null, CancellationToken ct = default)
        => _service.ListRecentAsync(take, locationId, ct);

    [HttpPost("open")]
    public Task<DailyClosingDto> Open([FromBody] OpenDayRequest req, CancellationToken ct)
        => _service.OpenDayAsync(req, ct);

    [HttpPost("movement")]
    public Task<CashMovementDto> RecordMovement([FromBody] RecordMovementRequest req, CancellationToken ct)
        => _service.RecordMovementAsync(req, ct);

    /// <summary>Fechar caixa impacta relatórios fiscais — só Admin.</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Admin")]
    public Task<DailyClosingDto> Close(Guid id, [FromBody] CloseDayRequest req, CancellationToken ct)
        => _service.CloseDayAsync(id, req, ct);
}
