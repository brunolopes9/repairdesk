using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 690: Definições → Pagamentos da loja (ifthenpay). Só o dono; as chaves nunca são devolvidas.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/loja/pagamentos")]
public sealed class PagamentosLojaController : ControllerBase
{
    private readonly IPagamentosLojaService _service;

    public PagamentosLojaController(IPagamentosLojaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagamentosLojaEstadoDto>> Estado(CancellationToken ct) => Ok(await _service.EstadoAsync(ct));

    [HttpPut]
    public async Task<ActionResult<PagamentosLojaEstadoDto>> Guardar([FromBody] GuardarPagamentosLojaRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAsync(req, ct));
}
