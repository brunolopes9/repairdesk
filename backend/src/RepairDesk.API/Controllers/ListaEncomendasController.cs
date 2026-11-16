using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Encomendas;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 687: lista de compras da oficina — o que é preciso encomendar, fora das reparações.</summary>
[ApiController]
[Authorize]
[Route("api/lista-encomendas")]
public sealed class ListaEncomendasController : ControllerBase
{
    private readonly IListaEncomendasService _service;
    public ListaEncomendasController(IListaEncomendasService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<ItemParaEncomendarDto>> Listar(CancellationToken ct) => _service.ListarAsync(ct);

    [HttpPost]
    public Task<ItemParaEncomendarDto> Criar([FromBody] ItemParaEncomendarRequest req, CancellationToken ct) => _service.CriarAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<ItemParaEncomendarDto> Atualizar(Guid id, [FromBody] ItemParaEncomendarRequest req, CancellationToken ct) => _service.AtualizarAsync(id, req, ct);

    [HttpPut("{id:guid}/estado")]
    public Task<ItemParaEncomendarDto> Estado(Guid id, [FromBody] EstadoItemRequest req, CancellationToken ct) => _service.MudarEstadoAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Apagar(Guid id, CancellationToken ct)
    {
        await _service.ApagarAsync(id, ct);
        return NoContent();
    }
}
