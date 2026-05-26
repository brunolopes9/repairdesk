using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 346 (Doc 83 Pillar 6): CRUD de tags de reparação (Urgente, Em garantia, etc).
/// Aplicáveis many-to-many via `PUT /api/reparacoes/{id}/tags`.
/// </summary>
[ApiController]
[Route("api/reparacao-tags")]
[Authorize]
public sealed class ReparacaoTagsController : ControllerBase
{
    private static readonly Regex HexColorRegex = new("^#([0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    private readonly IReparacaoTagRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _user;

    public ReparacaoTagsController(
        IReparacaoTagRepository repo,
        ITenantContext tenant,
        IAuditLogger audit,
        ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
        _user = user;
    }

    public sealed record TagDto(Guid Id, string Nome, string CorHex);
    public sealed record CreateOrUpdateTagRequest(string Nome, string? CorHex);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> List(CancellationToken ct)
    {
        var tags = await _repo.ListAsync(ct);
        return Ok(tags.Select(t => new TagDto(t.Id, t.Nome, t.CorHex)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TagDto>> Create([FromBody] CreateOrUpdateTagRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length < 1 || nome.Length > 40)
            return BadRequest(new { code = "invalid_name", message = "Nome entre 1 e 40 chars." });
        var cor = NormalizeCor(req.CorHex);

        var existing = await _repo.FindByNomeAsync(nome, ct);
        if (existing is not null) return Conflict(new { code = "duplicate", message = "Já existe tag com esse nome." });

        var tag = new ReparacaoTag
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome,
            CorHex = cor,
        };
        await _repo.AddAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Create, "ReparacaoTag", tag.Id, new { tag.Nome, tag.CorHex }, tenantId, _user.UserId, ct);
        return Ok(new TagDto(tag.Id, tag.Nome, tag.CorHex));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TagDto>> Update(Guid id, [FromBody] CreateOrUpdateTagRequest req, CancellationToken ct)
    {
        var tag = await _repo.FindByIdAsync(id, ct);
        if (tag is null) return NotFound();
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length < 1 || nome.Length > 40)
            return BadRequest(new { code = "invalid_name" });

        tag.Nome = nome;
        tag.CorHex = NormalizeCor(req.CorHex);
        await _repo.UpdateAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Update, "ReparacaoTag", tag.Id, new { tag.Nome, tag.CorHex }, tag.TenantId, _user.UserId, ct);
        return Ok(new TagDto(tag.Id, tag.Nome, tag.CorHex));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tag = await _repo.FindByIdAsync(id, ct);
        if (tag is null) return NotFound();
        await _repo.DeleteAsync(tag, ct);
        await _audit.LogAsync(AuditAction.Delete, "ReparacaoTag", tag.Id, new { tag.Nome }, tag.TenantId, _user.UserId, ct);
        return NoContent();
    }

    private static string NormalizeCor(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor)) return "#3F3F46";
        cor = cor.Trim();
        return HexColorRegex.IsMatch(cor) ? cor.ToUpperInvariant() : "#3F3F46";
    }
}
