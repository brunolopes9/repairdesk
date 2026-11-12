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
    private readonly IFinancasExcelImporter _importer;
    public FinancasController(IFinancasService service, IFinancasExcelImporter importer)
    {
        _service = service;
        _importer = importer;
    }

    /// <summary>Sprint 634: importa o histórico das folhas Despesas e Fixas do Excel de gestão (idempotente).</summary>
    [HttpPost("importar-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<FinancasImportResultado>> ImportarExcel(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { code = "sem_ficheiro" });
        await using var s = file.OpenReadStream();
        return Ok(await _importer.ImportarAsync(s, ct));
    }

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
