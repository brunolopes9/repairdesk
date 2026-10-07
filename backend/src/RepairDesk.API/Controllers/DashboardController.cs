using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Dashboard;

namespace RepairDesk.API.Controllers;

/// <summary>Dashboard do modelo novo (Doc 94): vendas do mês, em curso, alertas, stock e próximas entregas.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IPainelService _painel;

    public DashboardController(IPainelService painel) => _painel = painel;

    [HttpGet]
    public Task<PainelDto> Get(CancellationToken ct) => _painel.GetAsync(ct);
}
