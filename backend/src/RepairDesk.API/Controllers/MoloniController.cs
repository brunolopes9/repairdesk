using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Moloni;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 567: ligação ao Moloni ON (chave da API encriptada + séries por tipo de venda).</summary>
[ApiController]
[Route("api/moloni")]
[Authorize]
public class MoloniController : ControllerBase
{
    private readonly IMoloniService _service;
    public MoloniController(IMoloniService service) => _service = service;

    [HttpGet("estado")]
    public Task<MoloniEstadoDto> Estado(CancellationToken ct) => _service.EstadoAsync(ct);

    /// <summary>Guarda/valida a chave (vazia = mantém a atual) e as séries. Confirma que o módulo de API está ativo.</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    public Task<MoloniEstadoDto> Configurar([FromBody] MoloniConfigurarRequest req, CancellationToken ct) => _service.ConfigurarAsync(req, ct);

    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remover(CancellationToken ct)
    {
        await _service.RemoverAsync(ct);
        return NoContent();
    }
}
