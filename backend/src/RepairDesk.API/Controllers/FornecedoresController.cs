using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Fornecedores;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/fornecedores")]
[Authorize]
public class FornecedoresController : ControllerBase
{
    private readonly IFornecedorService _service;

    public FornecedoresController(IFornecedorService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<FornecedorDto>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => _service.ListAsync(includeInactive, ct);

    [HttpPost]
    public Task<FornecedorDto> Create([FromBody] FornecedorWriteRequest req, CancellationToken ct)
        => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<FornecedorDto> Update(Guid id, [FromBody] FornecedorWriteRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
