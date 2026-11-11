using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Rotina;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 615 (Doc 98): rotina pessoal — cada utilizador só vê e mexe na sua.</summary>
[ApiController]
[Authorize]
[Route("api/rotina")]
public sealed class RotinaController : ControllerBase
{
    private readonly IRotinaService _service;
    public RotinaController(IRotinaService service) => _service = service;

    [HttpGet("itens")]
    public async Task<ActionResult<IReadOnlyList<RotinaItemDto>>> Itens(CancellationToken ct) => Ok(await _service.ItensAsync(ct));

    [HttpPost("itens")]
    public async Task<ActionResult<RotinaItemDto>> Criar([FromBody] GuardarRotinaItemRequest req, CancellationToken ct)
        => Ok(await _service.CriarAsync(req, ct));

    [HttpPost("importar")]
    public async Task<ActionResult<IReadOnlyList<RotinaItemDto>>> Importar([FromBody] IReadOnlyList<GuardarRotinaItemRequest> itens, CancellationToken ct)
        => Ok(await _service.ImportarAsync(itens, ct));

    [HttpPut("itens/{id:guid}")]
    public async Task<ActionResult<RotinaItemDto>> Atualizar(Guid id, [FromBody] GuardarRotinaItemRequest req, CancellationToken ct)
        => Ok(await _service.AtualizarAsync(id, req, ct));

    [HttpDelete("itens/{id:guid}")]
    public async Task<IActionResult> Apagar(Guid id, CancellationToken ct)
    {
        await _service.ApagarAsync(id, ct);
        return NoContent();
    }

    [HttpGet("itens/{id:guid}/valores")]
    public async Task<ActionResult<IReadOnlyList<RotinaValorDto>>> Valores(Guid id, CancellationToken ct) => Ok(await _service.ValoresAsync(id, ct));

    /// <summary>O dia (hoje por omissão): itens, estado, bloco atual e próximo.</summary>
    [HttpGet("dia")]
    public async Task<ActionResult<RotinaDiaDto>> Dia([FromQuery] DateOnly? dia, CancellationToken ct) => Ok(await _service.DiaAsync(dia, ct));

    [HttpPost("itens/{id:guid}/dia/{dia}")]
    public async Task<ActionResult<RotinaDiaItemDto>> Registar(Guid id, DateOnly dia, [FromBody] RegistarRotinaRequest req, CancellationToken ct)
        => Ok(await _service.RegistarAsync(id, dia, req, ct));
}
