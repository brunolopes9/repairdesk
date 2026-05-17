using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Dashboard;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet]
    public Task<DashboardResponse> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return _service.GetCurrentMonthAsync(ct);
        return _service.GetRangeAsync(from.Value, to.Value, ct);
    }

    [HttpGet("financeiro")]
    public Task<FinanceiroResponse> GetFinanceiro([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return _service.GetFinanceiroCurrentMonthAsync(ct);
        return _service.GetFinanceiroRangeAsync(from.Value, to.Value, ct);
    }

    [HttpGet("alertas")]
    public Task<AlertasResponse> GetAlertas(CancellationToken ct) => _service.GetAlertasAsync(ct);

    [HttpGet("avaliacoes")]
    public Task<AvaliacoesDashboardResponse> GetAvaliacoes(CancellationToken ct) => _service.GetAvaliacoesAsync(ct);

    [HttpGet("tendencia")]
    public Task<TendenciaResponse> GetTendencia([FromQuery] int meses = 6, CancellationToken ct = default)
        => _service.GetTendenciaAsync(meses, ct);

    [HttpGet("top-reparacoes")]
    public Task<TopReparacoesResponse> GetTopReparacoes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 5,
        CancellationToken ct = default)
    {
        if (from is null || to is null)
            return _service.GetTopReparacoesCurrentMonthAsync(limit, ct);
        return _service.GetTopReparacoesAsync(from.Value, to.Value, limit, ct);
    }
}
