using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 480: CRUD for customer segment tags (VIP, Lead online, Company, etc.).
/// </summary>
[ApiController]
[Route("api/cliente-tags")]
[Authorize]
public sealed class ClienteTagsController : ControllerBase
{
    private static readonly Regex HexColorRegex = new("^#([0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    private readonly IClienteTagRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _user;

    public ClienteTagsController(
        IClienteTagRepository repo,
        ITenantContext tenant,
        IAuditLogger audit,
        ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
        _user = user;
    }

    public sealed record CreateOrUpdateClienteTagRequest(string Nome, string? CorHex);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteTagSummaryDto>>> List(CancellationToken ct)
    {
        var tags = await _repo.ListAsync(ct);
        return Ok(tags.Select(ToDto).ToList());
    }

    [HttpGet("segmento")]
    public async Task<ActionResult<ClienteCampanhaSegmentoDto>> Segmento([FromQuery] string? tagIds, CancellationToken ct)
    {
        if (!TryParseTagIds(tagIds, out var ids, out var error)) return BadRequest(error);
        return Ok(await BuildSegmentoDto(ids, ct));
    }

    [HttpGet("{id:guid}/segmento")]
    public async Task<ActionResult<ClienteCampanhaSegmentoDto>> SegmentoByTag(Guid id, CancellationToken ct) =>
        Ok(await BuildSegmentoDto(new[] { id }, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ClienteTagSummaryDto>> Create([FromBody] CreateOrUpdateClienteTagRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length < 1 || nome.Length > 40)
            return BadRequest(new { code = "invalid_name", message = "Nome entre 1 e 40 chars." });

        var existing = await _repo.FindByNomeAsync(nome, ct);
        if (existing is not null) return Conflict(new { code = "duplicate", message = "Ja existe etiqueta com esse nome." });

        var tag = new ClienteTag
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome,
            CorHex = NormalizeCor(req.CorHex),
        };
        await _repo.AddAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Create, "ClienteTag", tag.Id, new { tag.Nome, tag.CorHex }, tenantId, _user.UserId, ct);
        return Ok(ToDto(tag));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ClienteTagSummaryDto>> Update(Guid id, [FromBody] CreateOrUpdateClienteTagRequest req, CancellationToken ct)
    {
        var tag = await _repo.FindByIdAsync(id, ct);
        if (tag is null) return NotFound();

        var nome = (req.Nome ?? "").Trim();
        if (nome.Length < 1 || nome.Length > 40)
            return BadRequest(new { code = "invalid_name", message = "Nome entre 1 e 40 chars." });

        tag.Nome = nome;
        tag.CorHex = NormalizeCor(req.CorHex);
        await _repo.UpdateAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Update, "ClienteTag", tag.Id, new { tag.Nome, tag.CorHex }, tag.TenantId, _user.UserId, ct);
        return Ok(ToDto(tag));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tag = await _repo.FindByIdAsync(id, ct);
        if (tag is null) return NotFound();

        await _repo.DeleteAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Delete, "ClienteTag", tag.Id, new { tag.Nome }, tag.TenantId, _user.UserId, ct);
        return NoContent();
    }

    private static ClienteTagSummaryDto ToDto(ClienteTag tag) => new(tag.Id, tag.Nome, tag.CorHex);

    private async Task<ClienteCampanhaSegmentoDto> BuildSegmentoDto(IReadOnlyList<Guid> tagIds, CancellationToken ct)
    {
        var (clientes, totalSegmento, totalElegiveis) = await _repo.GetSegmentoAsync(tagIds, ct);
        return new ClienteCampanhaSegmentoDto(
            tagIds,
            totalSegmento,
            totalElegiveis,
            clientes.Select(ToClienteDto).ToList());
    }

    private static ClienteDto ToClienteDto(Cliente c) =>
        new(
            c.Id,
            c.Nome,
            c.Telefone,
            c.Email,
            c.Nif,
            c.Notas,
            c.CreatedAt,
            c.UpdatedAt,
            c.NotaImportante,
            c.ContactoPreferido,
            c.AceitaMarketing,
            c.NaoContactar,
            c.TagAssignments
                .Where(a => a.ClienteTag is not null)
                .OrderBy(a => a.ClienteTag!.Nome)
                .Select(a => new ClienteTagSummaryDto(a.ClienteTag!.Id, a.ClienteTag.Nome, a.ClienteTag.CorHex))
                .ToList());

    private static bool TryParseTagIds(string? raw, out IReadOnlyList<Guid> tagIds, out object? error)
    {
        tagIds = Array.Empty<Guid>();
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = new { code = "invalid_tag_ids", message = "Escolhe pelo menos uma etiqueta." };
            return false;
        }

        var ids = new List<Guid>();
        foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                error = new { code = "invalid_tag_ids", message = "tagIds deve ser CSV de GUIDs validos." };
                return false;
            }
            ids.Add(id);
        }

        var distinct = ids.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            error = new { code = "invalid_tag_ids", message = "Escolhe pelo menos uma etiqueta." };
            return false;
        }
        if (distinct.Length > 20)
        {
            error = new { code = "too_many_tags", message = "Escolhe no maximo 20 etiquetas por segmento." };
            return false;
        }

        tagIds = distinct;
        return true;
    }

    private static string NormalizeCor(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor)) return "#3F3F46";
        cor = cor.Trim();
        return HexColorRegex.IsMatch(cor) ? cor.ToUpperInvariant() : "#3F3F46";
    }
}
