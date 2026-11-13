using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 656: encomendas da loja online — só o dono (Admin).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/loja/encomendas")]
public sealed class LojaEncomendasController : ControllerBase
{
    private readonly IEncomendaLojaService _service;
    public LojaEncomendasController(IEncomendaLojaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EncomendaLojaDto>>> List(CancellationToken ct) => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EncomendaLojaDto>> Get(Guid id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}/estado")]
    public async Task<ActionResult<EncomendaLojaDto>> MudarEstado(Guid id, [FromBody] MudarEstadoEncomendaRequest req, CancellationToken ct)
        => Ok(await _service.MudarEstadoAsync(id, req, ct));
}
