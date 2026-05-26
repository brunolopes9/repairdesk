using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 359 (Doc 83): CRUD de templates de modelo. O conteúdo partilhado (descrição,
/// specs, preço bateria, imagens marketing) é definido 1× aqui; as unidades (Product)
/// herdam via Product.ModelId. Define-se o modelo uma vez ao importar um lote.
/// </summary>
[ApiController]
[Route("api/product-models")]
[Authorize]
public sealed class ProductModelsController : ControllerBase
{
    private readonly IProductModelRepository _repo;
    private readonly ITenantContext _tenant;

    public ProductModelsController(IProductModelRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public sealed record ModelImageDto(string Url, string? Alt, int Ordem);
    public sealed record ModelDto(
        Guid Id, string Brand, string Model, string? DescriptionMarkdown, string? SpecsJson,
        int? BatteryUpgradePriceCents, ProductCategory Category, string? Series, bool Active,
        int UnitsCount, IReadOnlyList<ModelImageDto> Images);
    public sealed record CreateOrUpdateModelRequest(
        string Brand, string Model, string? DescriptionMarkdown, string? SpecsJson,
        int? BatteryUpgradePriceCents, ProductCategory? Category, string? Series, bool? Active);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModelDto>>> List(CancellationToken ct)
    {
        var models = await _repo.ListAsync(ct);
        var dtos = new List<ModelDto>(models.Count);
        foreach (var m in models)
            dtos.Add(await MapAsync(m, ct));
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ModelDto>> Get(Guid id, CancellationToken ct)
    {
        var m = await _repo.FindByIdAsync(id, ct);
        return m is null ? NotFound() : Ok(await MapAsync(m, ct));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ModelDto>> Create([FromBody] CreateOrUpdateModelRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var brand = (req.Brand ?? "").Trim();
        var model = (req.Model ?? "").Trim();
        if (brand.Length is < 1 or > 60 || model.Length is < 1 or > 80)
            return BadRequest(new { code = "invalid_key", message = "Brand 1-60 e Model 1-80 chars." });
        if (req.BatteryUpgradePriceCents is < 0)
            return BadRequest(new { code = "invalid_price" });

        var existing = await _repo.FindByBrandModelAsync(brand, model, ct);
        if (existing is not null)
            return Conflict(new { code = "duplicate", message = $"Já existe modelo {brand} {model}." });

        var entity = new ProductModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Brand = brand,
            Model = model,
            DescriptionMarkdown = req.DescriptionMarkdown,
            SpecsJson = req.SpecsJson,
            BatteryUpgradePriceCents = req.BatteryUpgradePriceCents,
            Category = req.Category ?? ProductCategory.Phone,
            Series = string.IsNullOrWhiteSpace(req.Series) ? null : req.Series.Trim(),
            Active = req.Active ?? true,
        };
        await _repo.AddAsync(entity, ct);
        return Ok(await MapAsync(entity, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ModelDto>> Update(Guid id, [FromBody] CreateOrUpdateModelRequest req, CancellationToken ct)
    {
        var m = await _repo.FindByIdAsync(id, ct);
        if (m is null) return NotFound();
        if (req.BatteryUpgradePriceCents is < 0) return BadRequest(new { code = "invalid_price" });

        if (!string.IsNullOrWhiteSpace(req.Brand)) m.Brand = req.Brand.Trim();
        if (!string.IsNullOrWhiteSpace(req.Model)) m.Model = req.Model.Trim();
        m.DescriptionMarkdown = req.DescriptionMarkdown;
        m.SpecsJson = req.SpecsJson;
        m.BatteryUpgradePriceCents = req.BatteryUpgradePriceCents;
        if (req.Category is { } cat) m.Category = cat;
        m.Series = string.IsNullOrWhiteSpace(req.Series) ? null : req.Series.Trim();
        if (req.Active is { } active) m.Active = active;
        await _repo.SaveAsync(ct);
        return Ok(await MapAsync(m, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var m = await _repo.FindByIdAsync(id, ct);
        if (m is null) return NotFound();
        var units = await _repo.CountUnitsAsync(id, ct);
        if (units > 0)
            return Conflict(new { code = "has_units", message = $"{units} unidades ligadas. Desliga-as antes de apagar o modelo." });
        await _repo.DeleteAsync(m, ct);
        return NoContent();
    }

    private async Task<ModelDto> MapAsync(ProductModel m, CancellationToken ct) => new(
        m.Id, m.Brand, m.Model, m.DescriptionMarkdown, m.SpecsJson,
        m.BatteryUpgradePriceCents, m.Category, m.Series, m.Active,
        await _repo.CountUnitsAsync(m.Id, ct),
        m.Images.OrderBy(i => i.Ordem).Select(i => new ModelImageDto(i.Url, i.Alt, i.Ordem)).ToList());
}
