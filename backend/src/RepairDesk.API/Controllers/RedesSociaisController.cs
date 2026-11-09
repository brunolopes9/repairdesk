using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Conteudo.Redes;

namespace RepairDesk.API.Controllers;

public sealed record AutoPublicarRequest(bool Ligado);
public sealed record UrlLigarDto(string Url);
public sealed record TokenThreadsRequest(string Token);

/// <summary>
/// Sprint 599 (Doc 95 Fase B): ligar as contas Facebook/Instagram/Threads e o interruptor da
/// publicação automática — só o dono. Os tokens e secrets nunca saem do servidor.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/redes")]
public sealed class RedesSociaisController : ControllerBase
{
    private readonly IRedesSociaisService _service;

    public RedesSociaisController(IRedesSociaisService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<RedesSociaisEstadoDto>> Estado(CancellationToken ct)
        => Ok(await _service.EstadoAsync(ct));

    [HttpPut("apps")]
    public async Task<ActionResult<RedesSociaisEstadoDto>> GuardarApps([FromBody] GuardarAppsRedesRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAppsAsync(req, ct));

    [HttpPut("auto-publicar")]
    public async Task<ActionResult<RedesSociaisEstadoDto>> AutoPublicar([FromBody] AutoPublicarRequest req, CancellationToken ct)
        => Ok(await _service.DefinirAutoPublicarAsync(req.Ligado, ct));

    [HttpGet("{plataforma:regex(^(meta|threads)$)}/url-ligar")]
    public async Task<ActionResult<UrlLigarDto>> UrlLigar(string plataforma, CancellationToken ct)
        => Ok(new UrlLigarDto(await _service.UrlLigarAsync(plataforma, ct)));

    [HttpPut("threads/token")]
    public async Task<ActionResult<RedesSociaisEstadoDto>> LigarThreadsComToken([FromBody] TokenThreadsRequest req, CancellationToken ct)
        => Ok(await _service.LigarThreadsComTokenAsync(req.Token, ct));

    [HttpDelete("ligacoes/{rede}")]
    public async Task<ActionResult<RedesSociaisEstadoDto>> Desligar(RedeSocial rede, CancellationToken ct)
        => Ok(await _service.DesligarAsync(rede, ct));

    /// <summary>
    /// Volta do diálogo da Meta/Threads. Anónimo (é o browser a regressar de outro site); a
    /// autorização é o <c>state</c> assinado com tenant + utilizador + expiração.
    /// </summary>
    [HttpGet("{plataforma:regex(^(meta|threads)$)}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        string plataforma, [FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error, [FromQuery(Name = "error_reason")] string? errorReason, CancellationToken ct)
        => Redirect(await _service.CallbackAsync(plataforma, code, state, error ?? errorReason, ct));
}
