using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 435 (Doc 90 screenshot Services): CRUD do catálogo de serviços (mão-de-obra
/// pré-definida). Bruno escolhe "Bateria iPhone 13 — €40, garantia 2 anos" em vez
/// de re-escrever cada vez.
/// </summary>
[ApiController]
[Authorize]
[Route("api/services")]
public sealed class ServiceItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ServiceItemsController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public sealed record ServiceItemDto(
        Guid Id,
        string Nome,
        string? Descricao,
        int PrecoCents,
        int GarantiaDiasCliente,
        bool Activo);

    public sealed record CreateOrUpdateServiceItemRequest(
        string Nome,
        string? Descricao,
        int PrecoCents,
        int GarantiaDiasCliente,
        bool Activo = true);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceItemDto>>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.ServiceItems.AsNoTracking();
        if (!includeInactive) q = q.Where(s => s.Activo);
        var items = await q.OrderBy(s => s.Nome).ToListAsync(ct);
        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServiceItemDto>> Get(Guid id, CancellationToken ct)
    {
        var s = await _db.ServiceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? NotFound() : Ok(ToDto(s));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ServiceItemDto>> Create([FromBody] CreateOrUpdateServiceItemRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var validation = Validate(req);
        if (validation is { } err) return BadRequest(err);

        var s = new ServiceItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = req.Nome.Trim(),
            Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim(),
            PrecoCents = req.PrecoCents,
            GarantiaDiasCliente = req.GarantiaDiasCliente,
            Activo = req.Activo,
        };
        await _db.ServiceItems.AddAsync(s, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = s.Id }, ToDto(s));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ServiceItemDto>> Update(Guid id, [FromBody] CreateOrUpdateServiceItemRequest req, CancellationToken ct)
    {
        var s = await _db.ServiceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        var validation = Validate(req);
        if (validation is { } err) return BadRequest(err);

        s.Nome = req.Nome.Trim();
        s.Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        s.PrecoCents = req.PrecoCents;
        s.GarantiaDiasCliente = req.GarantiaDiasCliente;
        s.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(s));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var s = await _db.ServiceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        _db.ServiceItems.Remove(s);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static object? Validate(CreateOrUpdateServiceItemRequest req)
    {
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is < 2 or > 120)
            return new { code = "nome_invalido", message = "Nome entre 2 e 120 caracteres." };
        if (req.PrecoCents < 0)
            return new { code = "preco_invalido", message = "Preço não pode ser negativo." };
        if (req.GarantiaDiasCliente is < 0 or > 3650)
            return new { code = "garantia_invalida", message = "Garantia entre 0 e 3650 dias." };
        return null;
    }

    private static ServiceItemDto ToDto(ServiceItem s) =>
        new(s.Id, s.Nome, s.Descricao, s.PrecoCents, s.GarantiaDiasCliente, s.Activo);
}
