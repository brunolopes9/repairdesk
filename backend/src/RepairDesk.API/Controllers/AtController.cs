using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Controllers;

[ApiController]
[Authorize]
[Route("api/at")]
public sealed class AtController : ControllerBase
{
    private readonly IAtNifLookupService _lookup;

    public AtController(IAtNifLookupService lookup)
    {
        _lookup = lookup;
    }

    [HttpGet("nif-lookup/{nif}")]
    public async Task<ActionResult<AtNifLookupResponse>> LookupNif(string nif, CancellationToken ct)
    {
        var clean = NifValidator.Normalize(nif);
        if (!NifValidator.IsValid(clean))
        {
            return UnprocessableEntity(new
            {
                code = "nif_invalid",
                detail = "NIF invalido. Verifica os 9 digitos e o check-digit."
            });
        }

        try
        {
            var result = await _lookup.LookupAsync(clean, ct);
            if (result is null)
                return NotFound(new { code = "at_nif_not_found", detail = "NIF nao encontrado na AT." });

            return Ok(new AtNifLookupResponse(
                result.Nif,
                result.Nome,
                result.Morada,
                result.Status,
                result.CheckedAtUtc));
        }
        catch (AtNifRateLimitExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                code = "at_rate_limit_exceeded",
                detail = $"Limite diario de consultas AT atingido ({ex.Limit})."
            });
        }
        catch (AtNifUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "at_unavailable",
                detail = "Servico da AT indisponivel. Podes guardar o NIF validado localmente."
            });
        }
    }
}

public sealed record AtNifLookupResponse(
    string Nif,
    string Nome,
    string? Morada,
    string Status,
    DateTimeOffset CheckedAtUtc);
