using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Parts;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 353 (Doc 83 Pillar 5): CRUD de kits de peças + apply-kit numa reparação.
/// Bruno usa para evitar 5 cliques por reparação típica (ecrã + adesivo + parafusos).
/// </summary>
[ApiController]
[Route("api/part-kits")]
[Authorize]
public sealed class PartKitsController : ControllerBase
{
    private readonly IPartKitRepository _repo;
    private readonly IPartService _partService;
    private readonly ITenantContext _tenant;

    public PartKitsController(IPartKitRepository repo, IPartService partService, ITenantContext tenant)
    {
        _repo = repo;
        _partService = partService;
        _tenant = tenant;
    }

    public sealed record KitItemDto(Guid PartId, string PartNome, string? PartSku, int Quantidade, int CustoUnitarioCents);
    public sealed record KitDto(Guid Id, string Nome, string? Descricao, IReadOnlyList<KitItemDto> Items, int CustoTotalCents);
    public sealed record KitItemInput(Guid PartId, int Quantidade);
    public sealed record CreateOrUpdateKitRequest(string Nome, string? Descricao, IReadOnlyList<KitItemInput> Items);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KitDto>>> List(CancellationToken ct)
    {
        var kits = await _repo.ListAsync(ct);
        return Ok(kits.Select(MapDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KitDto>> Get(Guid id, CancellationToken ct)
    {
        var kit = await _repo.FindByIdAsync(id, ct);
        return kit is null ? NotFound() : Ok(MapDto(kit));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<KitDto>> Create([FromBody] CreateOrUpdateKitRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is < 1 or > 80)
            return BadRequest(new { code = "invalid_name", message = "Nome entre 1 e 80 chars." });
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(new { code = "no_items", message = "Kit precisa de pelo menos 1 peça." });

        var existing = await _repo.FindByNomeAsync(nome, ct);
        if (existing is not null) return Conflict(new { code = "duplicate", message = "Já existe kit com esse nome." });

        var kit = new PartKit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome,
            Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim(),
            Items = req.Items
                .Where(i => i.Quantidade > 0)
                .Select(i => new PartKitItem
                {
                    Id = Guid.NewGuid(),
                    PartId = i.PartId,
                    Quantidade = i.Quantidade,
                })
                .ToList(),
        };
        await _repo.AddAsync(kit, ct);

        var withDetails = await _repo.FindByIdAsync(kit.Id, ct);
        return Ok(MapDto(withDetails!));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<KitDto>> Update(Guid id, [FromBody] CreateOrUpdateKitRequest req, CancellationToken ct)
    {
        var kit = await _repo.FindByIdAsync(id, ct);
        if (kit is null) return NotFound();
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is < 1 or > 80) return BadRequest(new { code = "invalid_name" });
        if (req.Items is null || req.Items.Count == 0) return BadRequest(new { code = "no_items" });

        kit.Nome = nome;
        kit.Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        kit.Items.Clear();
        foreach (var i in req.Items.Where(x => x.Quantidade > 0))
        {
            kit.Items.Add(new PartKitItem
            {
                Id = Guid.NewGuid(),
                PartKitId = kit.Id,
                PartId = i.PartId,
                Quantidade = i.Quantidade,
            });
        }
        await _repo.UpdateAsync(kit, ct);

        var withDetails = await _repo.FindByIdAsync(kit.Id, ct);
        return Ok(MapDto(withDetails!));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var kit = await _repo.FindByIdAsync(id, ct);
        if (kit is null) return NotFound();
        await _repo.DeleteAsync(kit, ct);
        return NoContent();
    }

    /// <summary>
    /// Aplica o kit a uma reparação: para cada item cria um PartMovimento de saída
    /// (UsoEmReparacao) reutilizando IPartService. Se uma peça falhar (stock
    /// insuficiente, peça inactiva), aborta o resto e devolve erro com os itens
    /// já aplicados, para o user saber o estado.
    /// </summary>
    public sealed record ApplyKitRequest(Guid ReparacaoId);
    public sealed record AppliedItemDto(Guid PartId, string PartNome, int Quantidade);
    public sealed record ApplyKitResult(IReadOnlyList<AppliedItemDto> Applied, string? FailedAt);

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<ApplyKitResult>> Apply(Guid id, [FromBody] ApplyKitRequest req, CancellationToken ct)
    {
        var kit = await _repo.FindByIdAsync(id, ct);
        if (kit is null) return NotFound(new { code = "kit_not_found" });

        var applied = new List<AppliedItemDto>();
        foreach (var item in kit.Items)
        {
            if (item.Part is null) continue;
            try
            {
                await _partService.AddMovimentoAsync(item.PartId, new CreatePartMovimentoRequest(
                    Quantidade: -item.Quantidade,
                    Motivo: PartMovimentoMotivo.UsoEmReparacao,
                    ReparacaoId: req.ReparacaoId,
                    Notas: $"Kit: {kit.Nome}"), ct);
                applied.Add(new AppliedItemDto(item.PartId, item.Part.Nome, item.Quantidade));
            }
            catch (Exception ex)
            {
                return Conflict(new ApplyKitResult(applied, $"{item.Part.Nome}: {ex.Message}"));
            }
        }
        return Ok(new ApplyKitResult(applied, null));
    }

    private static KitDto MapDto(PartKit kit)
    {
        var items = kit.Items
            .Where(i => i.Part is not null)
            .Select(i => new KitItemDto(
                i.PartId,
                i.Part!.Nome,
                i.Part.Sku,
                i.Quantidade,
                i.Part.CustoUnitarioCents))
            .ToList();
        var custoTotal = items.Sum(i => i.Quantidade * i.CustoUnitarioCents);
        return new KitDto(kit.Id, kit.Nome, kit.Descricao, items, custoTotal);
    }
}
