using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Auth;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>23/11: Loja online → Visitas — tudo o que as pessoas fazem no lopestech.pt (medição própria).</summary>
[ApiController]
[Route("api/loja/visitas")]
[Authorize(Policy = AppPolicies.RequireAdmin)]
public sealed class VisitasSiteController : ControllerBase
{
    private readonly IVisitasSiteService _visitas;
    public VisitasSiteController(IVisitasSiteService visitas) => _visitas = visitas;

    [HttpGet("resumo")]
    public async Task<ActionResult<VisitasResumoDto>> Resumo([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        var (d, a) = Periodo(de, ate);
        return Ok(await _visitas.ResumoAsync(d, a, ct));
    }

    [HttpGet("sessoes")]
    public async Task<ActionResult<IReadOnlyList<SessaoVisitaDto>>> Sessoes([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        var (d, a) = Periodo(de, ate);
        return Ok(await _visitas.SessoesAsync(d, a, ct));
    }

    [HttpGet("sessoes/{sessao}")]
    public async Task<ActionResult<IReadOnlyList<EventoVisitaDto>>> Sessao(string sessao, CancellationToken ct)
        => Ok(await _visitas.SessaoAsync(sessao, ct));

    /// <summary>26/11: «fui eu» — apaga a visita e todas as do mesmo visitante (as novas já não entram: cookie rd_interno).</summary>
    [HttpDelete("sessoes/{sessao}")]
    public async Task<IActionResult> NaoContar(string sessao, CancellationToken ct)
        => Ok(new { apagados = await _visitas.NaoContarAsync(sessao, ct) });

    private static (DateOnly, DateOnly) Periodo(DateOnly? de, DateOnly? ate)
    {
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")));
        return (de ?? hoje.AddDays(-6), ate ?? hoje);
    }
}
