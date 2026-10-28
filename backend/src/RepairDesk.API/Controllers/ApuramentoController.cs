using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 573: IVA e resultado por trimestre (dados financeiros — só o administrador).</summary>
[ApiController]
[Route("api/apuramento")]
[Authorize(Roles = "Admin")]
public class ApuramentoController : ControllerBase
{
    private readonly IApuramentoService _service;
    public ApuramentoController(IApuramentoService service) => _service = service;

    [HttpGet]
    public Task<ApuramentoDto> Trimestre([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
        => _service.TrimestreAsync(ano, trimestre, ct);

    /// <summary>Sprint 576: pressupostos das estimativas (início de atividade, contribuição atual, faturação anterior ao Mender).</summary>
    [HttpPut("pressupostos")]
    public async Task<IActionResult> Pressupostos([FromBody] PressupostosRequest req, CancellationToken ct = default)
    {
        await _service.DefinirPressupostosAsync(req, ct);
        return NoContent();
    }

    /// <summary>Sprint 580: lembretes fiscais com prazo (Dashboard e IVA e resultados).</summary>
    [HttpGet("lembretes")]
    public Task<IReadOnlyList<LembreteFiscalDto>> Lembretes(CancellationToken ct = default)
        => _service.LembretesAsync(ct);

    /// <summary>Sprint 580: "já revi o código de atividade do trading" (false volta a ativar o lembrete).</summary>
    [HttpPut("lembretes/codigo-trading")]
    public async Task<IActionResult> CodigoTradingRevisto([FromBody] CodigoTradingRevistoRequest req, CancellationToken ct = default)
    {
        await _service.MarcarCodigoTradingRevistoAsync(req.Revisto, ct);
        return NoContent();
    }

    /// <summary>Sprint 576: marcar o trimestre como declarado/pago (guarda os valores declarados).</summary>
    [HttpPost("fecho")]
    public Task<ApuramentoFechoDto> Fechar([FromBody] FecharTrimestreRequest req, CancellationToken ct = default)
        => _service.FecharAsync(req, ct);

    [HttpDelete("fecho")]
    public async Task<IActionResult> Reabrir([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
    {
        await _service.ReabrirAsync(ano, trimestre, ct);
        return NoContent();
    }
}

public sealed record CodigoTradingRevistoRequest(bool Revisto);
