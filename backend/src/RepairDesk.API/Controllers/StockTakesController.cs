using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.StockTakes;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 421 (Doc 90 Tier 1 #3): inventário físico. Admin only — gera ajustes
/// de stock que afectam directamente custo e relatório IVA.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/stock-takes")]
public sealed class StockTakesController : ControllerBase
{
    private readonly IStockTakeService _service;

    public StockTakesController(IStockTakeService service) => _service = service;

    /// <summary>Inventário aberto agora (ou null se não há nenhum).</summary>
    [HttpGet("current")]
    public async Task<ActionResult<StockTakeDto?>> Current(CancellationToken ct)
    {
        var current = await _service.GetCurrentAsync(ct);
        return current is null ? NoContent() : Ok(current);
    }

    /// <summary>Histórico recente (concluídos + cancelados + aberto, se houver).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockTakeDto>>> List([FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _service.ListRecentAsync(take, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockTakeDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    /// <summary>Abrir um novo inventário (snapshot de todas as Parts activas).</summary>
    [HttpPost]
    public async Task<ActionResult<StockTakeDto>> Open(CancellationToken ct)
        => Ok(await _service.OpenAsync(ct));

    /// <summary>Registar contagem física para uma peça do inventário.</summary>
    [HttpPut("{id:guid}/items/{partId:guid}")]
    public async Task<ActionResult<StockTakeItemDto>> Count(Guid id, Guid partId, [FromBody] CountItemRequest req, CancellationToken ct)
        => Ok(await _service.CountItemAsync(id, partId, req.QtdContada, ct));

    /// <summary>Fechar inventário: cria PartMovimentos de ajuste para diferenças != 0.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<StockTakeDto>> Close(Guid id, [FromBody] CloseStockTakeRequest req, CancellationToken ct)
        => Ok(await _service.CloseAsync(id, req.Notas, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<StockTakeDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));
}
