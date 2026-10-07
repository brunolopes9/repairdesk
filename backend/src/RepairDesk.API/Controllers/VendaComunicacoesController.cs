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
[Route("api/vendas/{vendaId:guid}/comunicacoes")]
public sealed class ReparacaoComunicacoesController : ControllerBase
{
    private readonly IVendaComunicacaoService _service;

    public ReparacaoComunicacoesController(IVendaComunicacaoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VendaComunicacaoDto>>> List(Guid vendaId, CancellationToken ct)
        => Ok(await _service.ListAsync(vendaId, ct));

    [HttpPost]
    public async Task<ActionResult<VendaComunicacaoDto>> Create(Guid vendaId, [FromBody] CreateComunicacaoRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(vendaId, req, ct);
        return Created($"/api/vendas/{vendaId}/comunicacoes/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid vendaId, Guid id, CancellationToken ct)
    {
        _ = vendaId;
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
