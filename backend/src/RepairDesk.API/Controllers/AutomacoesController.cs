using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 165: pequenos utilitários para a página /definicoes/automacoes do frontend.
/// Sprint 173: também serve o ingest-email per-tenant.
/// </summary>
[ApiController]
[Route("api/automacoes")]
[Authorize]
public class AutomacoesController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ITenantContext _tenant;
    private readonly AppDbContext _db;

    public AutomacoesController(IHttpClientFactory httpFactory, IConfiguration config, ITenantContext tenant, AppDbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _tenant = tenant;
        _db = db;
    }

    [HttpGet("n8n-status")]
    public async Task<IActionResult> N8nStatus(CancellationToken ct)
    {
        var url = _config["N8N_HEALTH_URL"] ?? "http://n8n:5678/healthz";
        try
        {
            using var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            using var res = await http.GetAsync(url, ct);
            return Ok(new { up = res.IsSuccessStatusCode, url });
        }
        catch
        {
            return Ok(new { up = false, url });
        }
    }

    /// <summary>
    /// Sprint 173: devolve o email único para forwarding (gera na 1ª chamada se NULL).
    /// Tenant configura forward Gmail → este email → RepairDesk processa.
    /// </summary>
    [HttpGet("ingest-email")]
    public async Task<IActionResult> GetIngestEmail(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        // Gerar slug auto se ainda NULL.
        if (string.IsNullOrWhiteSpace(tenant.IngestEmailSlug))
        {
            tenant.IngestEmailSlug = await GenerateUniqueSlugAsync(tenant.Name, ct);
            await _db.SaveChangesAsync(ct);
        }

        var domain = _config["EmailIngest:Domain"] ?? "ingest.repairdesk.app";
        return Ok(new
        {
            slug = tenant.IngestEmailSlug,
            email = $"faturas-{tenant.IngestEmailSlug}@{domain}",
            domain,
        });
    }

    /// <summary>Sprint 173: regenera o slug (caso tenant ache que está exposto).</summary>
    // Sprint 243 Fase A: regenerar slug quebra workflows existentes (emails antigos
    // deixam de ser entregues). Doc 72 §2 A.7.
    [HttpPost("ingest-email/regenerate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegenerateIngestEmail(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();
        tenant.IngestEmailSlug = await GenerateUniqueSlugAsync(tenant.Name, ct);
        await _db.SaveChangesAsync(ct);
        var domain = _config["EmailIngest:Domain"] ?? "ingest.repairdesk.app";
        return Ok(new { slug = tenant.IngestEmailSlug, email = $"faturas-{tenant.IngestEmailSlug}@{domain}" });
    }

    private async Task<string> GenerateUniqueSlugAsync(string baseName, CancellationToken ct)
    {
        var baseSlug = Slugify(baseName);
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "tenant";
        // Adiciona sufixo random 4-char para garantir unicidade.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..4].ToLowerInvariant();
            var candidate = $"{baseSlug}-{suffix}";
            var exists = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.IngestEmailSlug == candidate, ct);
            if (!exists) return candidate;
        }
        throw new InvalidOperationException("Não foi possível gerar slug único após 10 tentativas.");
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var lower = input.ToLowerInvariant().Trim();
        var safe = new System.Text.StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) safe.Append(c);
            else if (c is ' ' or '-' or '_') safe.Append('-');
        }
        var result = System.Text.RegularExpressions.Regex.Replace(safe.ToString(), @"-+", "-").Trim('-');
        return result.Length > 30 ? result[..30] : result;
    }
}
