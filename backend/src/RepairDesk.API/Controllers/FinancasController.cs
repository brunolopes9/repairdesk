using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Financas;

namespace RepairDesk.API.Controllers;

public sealed record ObjetivoRequest(decimal Objetivo);

/// <summary>Sprint 633 (Doc 99): Finanças do dono — só o Admin (inclui despesas pessoais).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/financas")]
public sealed class FinancasController : ControllerBase
{
    private readonly IFinancasService _service;
    public FinancasController(IFinancasService service) => _service = service;

    [HttpGet("mes")]
    public async Task<ActionResult<FinancasMesDto>> Mes([FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
        => Ok(await _service.MesAsync(ano, mes, ct));

    [HttpGet("ano")]
    public async Task<ActionResult<IReadOnlyList<FinancasResumoMesDto>>> Ano([FromQuery] int ano, CancellationToken ct)
        => Ok(await _service.AnoAsync(ano, ct));

    [HttpPut("objetivo")]
    public async Task<IActionResult> Objetivo([FromBody] ObjetivoRequest req, CancellationToken ct)
    {
        await _service.DefinirObjetivoAsync(req.Objetivo, ct);
        return NoContent();
    }
}
