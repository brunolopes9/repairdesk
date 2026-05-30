using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Comunicacoes;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 452 (Doc 91 ponto 1 — Conversas omnicanal v1): registo manual de
/// comunicações com cliente. Endpoints nested em /reparacoes para deixar claro
/// que o eixo é a reparação (não o cliente). Quando "Conversas" crescer para
/// visão por cliente, adicionamos /api/clientes/{id}/comunicacoes.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reparacoes/{reparacaoId:guid}/comunicacoes")]
public sealed class ReparacaoComunicacoesController : ControllerBase
{
    private readonly IReparacaoComunicacaoService _service;

    public ReparacaoComunicacoesController(IReparacaoComunicacaoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReparacaoComunicacaoDto>>> List(Guid reparacaoId, CancellationToken ct)
        => Ok(await _service.ListAsync(reparacaoId, ct));

    [HttpPost]
    public async Task<ActionResult<ReparacaoComunicacaoDto>> Create(Guid reparacaoId, [FromBody] CreateComunicacaoRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(reparacaoId, req, ct);
        return Created($"/api/reparacoes/{reparacaoId}/comunicacoes/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid reparacaoId, Guid id, CancellationToken ct)
    {
        _ = reparacaoId;
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
