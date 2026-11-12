using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Conteudo;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 598 (Doc 95): calendário de conteúdo das redes sociais — só o dono (Admin).
/// Aprovar/rejeitar existem SÓ aqui: a API key do Claude (<see cref="ExternalConteudoController"/>)
/// nunca consegue aprovar o próprio conteúdo.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/conteudo")]
public sealed class ConteudoController : ControllerBase
{
    private readonly IConteudoService _service;

    public ConteudoController(IConteudoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConteudoPublicacaoDto>>> List(
        [FromQuery] ConteudoEstado[]? estados, [FromQuery] DateTime? de, [FromQuery] DateTime? ate, CancellationToken ct)
        => Ok(await _service.ListAsync(estados, de, ate, ct));

    [HttpGet("resumo")]
    public async Task<ActionResult<ConteudoResumoDto>> Resumo(CancellationToken ct)
        => Ok(await _service.ResumoAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Create([FromBody] GuardarPublicacaoRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Update(Guid id, [FromBody] GuardarPublicacaoRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/submeter")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Submeter(Guid id, CancellationToken ct)
        => Ok(await _service.SubmeterAsync(id, ct));

    [HttpPost("{id:guid}/aprovar")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Aprovar(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] HoraPublicacaoRequest? hora, CancellationToken ct)
        => Ok(await _service.AprovarAsync(id, hora, ct));

    /// <summary>Sprint 610: mudar a hora (ou «Publicar já») sem voltar a «Por aprovar».</summary>
    [HttpPatch("{id:guid}/hora")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> MudarHora(Guid id, [FromBody] HoraPublicacaoRequest req, CancellationToken ct)
        => Ok(await _service.MudarHoraAsync(id, req, ct));

    [HttpPost("{id:guid}/rejeitar")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Rejeitar(Guid id, [FromBody] RejeitarPublicacaoRequest req, CancellationToken ct)
        => Ok(await _service.RejeitarAsync(id, req.Motivo, ct));

    [HttpPost("{id:guid}/cancelar")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Cancelar(Guid id, CancellationToken ct)
        => Ok(await _service.CancelarAsync(id, ct));

    /// <summary>Sprint 629: já saiu mas foi apagada nas redes — deixa de contar.</summary>
    [HttpPost("{id:guid}/removida")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Removida(Guid id, CancellationToken ct)
        => Ok(await _service.MarcarRemovidaAsync(id, ct));

    [HttpPut("{id:guid}/destinos/{rede}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> AtualizarDestino(Guid id, RedeSocial rede, [FromBody] AtualizarDestinoRequest req, CancellationToken ct)
        => Ok(await _service.AtualizarDestinoAsync(id, rede, req, ct));

    [HttpPut("{id:guid}/tiktok")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> OpcoesTikTok(Guid id, [FromBody] GuardarOpcoesTikTokRequest req, CancellationToken ct)
        => Ok(await _service.GuardarOpcoesTikTokAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("biblioteca")]
    public async Task<ActionResult<IReadOnlyList<ConteudoFicheiroDto>>> Biblioteca([FromQuery] bool soPorProcessar, CancellationToken ct)
        => Ok(await _service.BibliotecaAsync(soPorProcessar, ct));

    [HttpPost("ficheiros/upload")]
    public async Task<ActionResult<UploadTicketDto>> PedirUpload([FromBody] PedirUploadRequest req, CancellationToken ct)
        => Ok(await _service.PedirUploadAsync(req, ct));

    [HttpPost("ficheiros/{id:guid}/confirmar")]
    public async Task<ActionResult<ConteudoFicheiroDto>> ConfirmarUpload(Guid id, [FromBody] ConfirmarUploadRequest req, CancellationToken ct)
        => Ok(await _service.ConfirmarUploadAsync(id, req, ct));

    [HttpPost("ficheiros/{id:guid}/processado")]
    public async Task<ActionResult<ConteudoFicheiroDto>> MarcarProcessado(Guid id, CancellationToken ct)
        => Ok(await _service.MarcarProcessadoAsync(id, ct));

    [HttpDelete("ficheiros/{id:guid}")]
    public async Task<IActionResult> DeleteFicheiro(Guid id, CancellationToken ct)
    {
        await _service.DeleteFicheiroAsync(id, ct);
        return NoContent();
    }
}

/// <summary>
/// Sprint 598 (Doc 95): proxy de binários quando o storage não dá URLs pré-assinados (dev local).
/// Sem JWT porque o &lt;video&gt; e o PUT do browser não o enviam — a autorização é a assinatura.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/conteudo/ficheiros")]
public sealed class ConteudoBinarioController : ControllerBase
{
    private const long MaxProxyBytes = 500L * 1024 * 1024;

    private readonly IConteudoService _service;
    private readonly IConteudoLinkSigner _signer;

    public ConteudoBinarioController(IConteudoService service, IConteudoLinkSigner signer)
    {
        _service = service;
        _signer = signer;
    }

    [HttpGet("{id:guid}/binario")]
    public async Task<IActionResult> Ler(Guid id, [FromQuery] string acao, [FromQuery] long expires, [FromQuery] string sig, CancellationToken ct)
    {
        _signer.Validate(id, acao == "down" ? "down" : "invalido", expires, sig);
        var (stream, contentType, nome) = await _service.AbrirBinarioAsync(id, ct);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(stream, contentType, nome, enableRangeProcessing: true);
    }

    [HttpPut("{id:guid}/binario")]
    [RequestSizeLimit(MaxProxyBytes)]
    public async Task<IActionResult> Gravar(Guid id, [FromQuery] string acao, [FromQuery] long expires, [FromQuery] string sig, CancellationToken ct)
    {
        _signer.Validate(id, acao == "up" ? "up" : "invalido", expires, sig);
        await _service.GravarBinarioAsync(id, Request.Body, Request.ContentType ?? "", ct);
        return NoContent();
    }
}

/// <summary>
/// Sprint 598 (Doc 95): acesso do Claude (API key com scope «conteudo»). Cria, submete e regista
/// publicações e lê a biblioteca de vídeos brutos. NÃO aprova — isso é só do dono.
/// </summary>
[ApiController]
[Route("api/external/conteudo")]
[Authorize(AuthenticationSchemes = ApiKeyAuthHandler.SchemeName)]
[ApiScope("conteudo")]
[EnableRateLimiting("external-apikey")]
public sealed class ExternalConteudoController : ControllerBase
{
    private readonly IConteudoService _service;

    public ExternalConteudoController(IConteudoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConteudoPublicacaoDto>>> List(
        [FromQuery] ConteudoEstado[]? estados, [FromQuery] DateTime? de, [FromQuery] DateTime? ate, CancellationToken ct)
        => Ok(await _service.ListAsync(estados, de, ate, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Create([FromBody] GuardarPublicacaoRequest req, CancellationToken ct)
        => Ok(await _service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Update(Guid id, [FromBody] GuardarPublicacaoRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/submeter")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Submeter(Guid id, CancellationToken ct)
        => Ok(await _service.SubmeterAsync(id, ct));

    [HttpPost("{id:guid}/cancelar")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Cancelar(Guid id, CancellationToken ct)
        => Ok(await _service.CancelarAsync(id, ct));

    /// <summary>Sprint 629: já saiu mas foi apagada nas redes — deixa de contar.</summary>
    [HttpPost("{id:guid}/removida")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> Removida(Guid id, CancellationToken ct)
        => Ok(await _service.MarcarRemovidaAsync(id, ct));

    [HttpPut("{id:guid}/destinos/{rede}")]
    public async Task<ActionResult<ConteudoPublicacaoDto>> AtualizarDestino(Guid id, RedeSocial rede, [FromBody] AtualizarDestinoRequest req, CancellationToken ct)
        => Ok(await _service.AtualizarDestinoAsync(id, rede, req, ct));

    [HttpGet("biblioteca")]
    public async Task<ActionResult<IReadOnlyList<ConteudoFicheiroDto>>> Biblioteca([FromQuery] bool soPorProcessar, CancellationToken ct)
        => Ok(await _service.BibliotecaAsync(soPorProcessar, ct));

    [HttpPost("ficheiros/upload")]
    public async Task<ActionResult<UploadTicketDto>> PedirUpload([FromBody] PedirUploadRequest req, CancellationToken ct)
        => Ok(await _service.PedirUploadAsync(req, ct));

    [HttpPost("ficheiros/{id:guid}/confirmar")]
    public async Task<ActionResult<ConteudoFicheiroDto>> ConfirmarUpload(Guid id, [FromBody] ConfirmarUploadRequest req, CancellationToken ct)
        => Ok(await _service.ConfirmarUploadAsync(id, req, ct));

    [HttpPost("ficheiros/{id:guid}/processado")]
    public async Task<ActionResult<ConteudoFicheiroDto>> MarcarProcessado(Guid id, CancellationToken ct)
        => Ok(await _service.MarcarProcessadoAsync(id, ct));
}
