using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 354 (Doc 83 Pillar 9): endpoint público do widget de repair-request.
/// **Sem autenticação** — segurança via IntakeSlug não-adivinhável + rate
/// limiting + cap por IP (anti-spam). Cria um lead Pendente que o staff revê.
/// </summary>
[ApiController]
[Route("api/public/repair-requests")]
[AllowAnonymous]
[EnableRateLimiting("public-portal")]
public class PublicRepairRequestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRepairRequestRepository _repo;
    private readonly ILogger<PublicRepairRequestController> _logger;

    public PublicRepairRequestController(AppDbContext db, IRepairRequestRepository repo, ILogger<PublicRepairRequestController> logger)
    {
        _db = db;
        _repo = repo;
        _logger = logger;
    }

    public sealed record SubmitRequest(string Nome, string? Email, string? Telefone, string Equipamento, string Descricao, string? Website);
    public sealed record SubmitResult(bool Ok);

    /// <summary>GET leve: confirma que o widget está activo + devolve nome/cor da loja para branding.</summary>
    public sealed record WidgetInfo(string LojaNome, string? PrimaryColor);

    [HttpGet("{intakeSlug}")]
    public async Task<ActionResult<WidgetInfo>> Info(string intakeSlug, CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IntakeSlug == intakeSlug && t.IsActive, ct);
        if (tenant is null) return NotFound();
        return Ok(new WidgetInfo(tenant.LegalName ?? tenant.Name, tenant.PrimaryColor));
    }

    [HttpPost("{intakeSlug}")]
    public async Task<ActionResult<SubmitResult>> Submit(string intakeSlug, [FromBody] SubmitRequest req, CancellationToken ct)
    {
        // Honeypot: campo "Website" é escondido no form via CSS. Bots preenchem-no.
        // Devolvemos 200 OK falso para não dar feedback ao bot.
        if (!string.IsNullOrWhiteSpace(req.Website))
        {
            _logger.LogInformation("RepairRequest honeypot disparou para slug {Slug}", intakeSlug);
            return Ok(new SubmitResult(true));
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IntakeSlug == intakeSlug && t.IsActive, ct);
        if (tenant is null) return NotFound(new { code = "widget_not_found" });

        var nome = (req.Nome ?? "").Trim();
        var equipamento = (req.Equipamento ?? "").Trim();
        var descricao = (req.Descricao ?? "").Trim();
        if (nome.Length is < 2 or > 120) return BadRequest(new { code = "invalid_nome" });
        if (equipamento.Length is < 2 or > 120) return BadRequest(new { code = "invalid_equipamento" });
        if (descricao.Length is < 5 or > 2000) return BadRequest(new { code = "invalid_descricao" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        // Cap anti-abuso: máx 5 pedidos por IP por tenant em 24h.
        var recent = await _repo.CountRecentByIpAsync(tenant.Id, ip, TimeSpan.FromHours(24), ct);
        if (recent >= 5)
        {
            _logger.LogWarning("RepairRequest IP {Ip} excedeu cap 24h no tenant {Tenant}", ip, tenant.Id);
            return StatusCode(429, new { code = "rate_limited", message = "Demasiados pedidos. Tenta mais tarde ou liga-nos." });
        }

        var entity = new RepairRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Nome = nome,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim(),
            Equipamento = equipamento,
            Descricao = descricao,
            SourceIp = ip.Length > 45 ? ip[..45] : ip,
        };
        await _repo.AddAsync(entity, ct);
        return Ok(new SubmitResult(true));
    }
}
