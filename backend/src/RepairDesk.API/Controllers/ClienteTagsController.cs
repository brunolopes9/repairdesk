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

    private static string NormalizeCor(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor)) return "#3F3F46";
        cor = cor.Trim();
        return HexColorRegex.IsMatch(cor) ? cor.ToUpperInvariant() : "#3F3F46";
    }
}
