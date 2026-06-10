using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Fornecedores;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/fornecedores")]
[Authorize]
public class FornecedoresController : ControllerBase
{
    private readonly IFornecedorService _service;
    private readonly IFornecedorRepository _repo;

    public FornecedoresController(IFornecedorService service, IFornecedorRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    [HttpGet]
    public Task<IReadOnlyList<FornecedorDto>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => _service.ListAsync(includeInactive, ct);

    // Sprint 548 (Doc 93 #3): histórico consolidado — compras, despesas, importações, última
    // compra e taxa de defeito a 12m, tudo numa vista (o "Histórico de Fornecedores" do Moloni).
    // Consulta read-only de agregação pura → vai direto ao repositório, sem camada de service.
    [HttpGet("{id:guid}/historico")]
    public async Task<ActionResult<FornecedorHistorico>> Historico(Guid id, CancellationToken ct)
    {
        var historico = await _repo.GetHistoricoAsync(id, ct);
        return historico is null ? NotFound() : historico;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public Task<FornecedorDto> Create([FromBody] FornecedorWriteRequest req, CancellationToken ct)
        => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<FornecedorDto> Update(Guid id, [FromBody] FornecedorWriteRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
