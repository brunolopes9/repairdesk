using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Imei;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 390 (Doc 04): lookup TAC→modelo (auto-detetar marca/modelo a partir do IMEI) + import
/// da base TAC. Lookup é operacional (qualquer empregado); o import da base é só Admin.
/// </summary>
[ApiController]
[Route("api/imei")]
[Authorize]
public sealed class ImeiController : ControllerBase
{
    private readonly ITacLookupService _tac;
    public ImeiController(ITacLookupService tac) => _tac = tac;

    /// <summary>Resolve marca+modelo de um IMEI. found=false quando a base não tem o TAC.</summary>
    [HttpGet("lookup")]
    public ActionResult<TacLookupResult> Lookup([FromQuery] string imei)
        => Ok(_tac.Resolve(imei));

    /// <summary>Estado da base TAC (nº de entradas carregadas).</summary>
    [HttpGet("tac-db/status")]
    public ActionResult<object> Status() => Ok(new { count = _tac.Count });

    /// <summary>
    /// Importa a base TAC de um CSV "tac;marca;modelo" (uma linha por TAC). Substitui a base atual.
    /// Usar o dump aberto Osmocom (CC-BY-SA) ou MoazEb (MIT) convertido para este formato.
    /// </summary>
    [HttpPost("tac-db/import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Ficheiro vazio." });
        await using var stream = file.OpenReadStream();
        var count = await _tac.ImportCsvAsync(stream, ct);
        return Ok(new { count });
    }
}
