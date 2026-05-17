using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.PriceTable;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/price-table")]
[Authorize]
public class PriceTableController : ControllerBase
{
    private readonly IPriceTableService _service;
    public PriceTableController(IPriceTableService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<PriceTableEntryDto>> Search(
        [FromQuery] string? q,
        [FromQuery] DeviceCategory? categoria,
        [FromQuery] string? marca,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(q, categoria, marca, page, pageSize, ct);

    [HttpGet("marcas")]
    public Task<IReadOnlyList<string>> Marcas(CancellationToken ct) => _service.ListMarcasAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<PriceTableEntryDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PriceTableEntryDto>> Create([FromBody] CreatePriceEntryRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<PriceTableEntryDto> Update(Guid id, [FromBody] UpdatePriceEntryRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("import")]
    public Task<ImportPriceTableResponse> Import([FromBody] ImportPriceTableRequest req, CancellationToken ct)
        => _service.ImportCsvAsync(req.Csv, ct);
}
