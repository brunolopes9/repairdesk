using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Products;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<ProductDto>> Search(
        [FromQuery] string? search,
        [FromQuery] string? brand,
        [FromQuery] bool? lojaOnline,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(search, brand, lojaOnline, includeInactive, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public Task<ProductDto> Create([FromBody] ProductWriteRequest req, CancellationToken ct) => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, [FromBody] ProductWriteRequest req, CancellationToken ct) => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
