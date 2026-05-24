using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Controllers;

public sealed record SetAnthropicKeyRequest(string ApiKey);

/// <summary>
/// Sprint 167a: endpoint para Bruno ver gasto LLM Anthropic per-tenant.
/// Sprint 167b: também inclui quota check (plan + used/limit).
/// </summary>
[ApiController]
[Route("api/llm-usage")]
[Authorize]
public class LlmUsageController : ControllerBase
{
    private readonly ILlmUsageRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ILlmQuotaService _quota;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAuditLogger _audit;

    public LlmUsageController(
        ILlmUsageRepository repo,
        ITenantContext tenant,
        ILlmQuotaService quota,
        ITenantRepository tenants,
        ISecretProtector secrets,
        IHttpClientFactory httpFactory,
        IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _quota = quota;
        _tenants = tenants;
        _secrets = secrets;
        _httpFactory = httpFactory;
        _audit = audit;
    }

    /// <summary>
    /// Sprint 168: salva Anthropic API key encriptada para este tenant.
    /// Valida com chamada a Anthropic /v1/models antes de gravar.
    /// </summary>
    // Sprint 243 Fase A: BYOK Anthropic é credencial sensível (custos $$ associados,
    // acesso a API externa). Doc 72 §2 A.6.
    [HttpPost("anthropic-key")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetAnthropicKey([FromBody] SetAnthropicKeyRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.ApiKey) || !req.ApiKey.StartsWith("sk-ant-", StringComparison.Ordinal))
            return BadRequest(new { code = "invalid_key", detail = "Formato inválido. Keys Anthropic começam por 'sk-ant-'." });

        // Valida com Anthropic — chamada barata a /v1/models.
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        using var validateReq = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        validateReq.Headers.Add("x-api-key", req.ApiKey);
        validateReq.Headers.Add("anthropic-version", "2023-06-01");
        try
        {
            using var resp = await http.SendAsync(validateReq, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return BadRequest(new { code = "key_rejected", detail = $"Anthropic devolveu {(int)resp.StatusCode}. Verifica se a key está activa e tem créditos." });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = "validation_failed", detail = $"Não foi possível validar com Anthropic: {ex.Message}" });
        }

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);
        if (tenant is null) return NotFound();
        tenant.AnthropicApiKeyCipherText = _secrets.Protect(req.ApiKey);
        tenant.AnthropicValidatedAt = DateTime.UtcNow;
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), tenantId, new { operation = "anthropic_key_set" }, ct: ct);
        return Ok(new { configured = true, validatedAt = tenant.AnthropicValidatedAt });
    }

    /// <summary>Remove a Anthropic key — LLM features ficam desactivadas.</summary>
    [HttpDelete("anthropic-key")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveAnthropicKey(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var tenant = await _tenants.FindByIdAsync(tenantId, ct);
        if (tenant is null) return NotFound();
        tenant.AnthropicApiKeyCipherText = null;
        tenant.AnthropicValidatedAt = null;
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), tenantId, new { operation = "anthropic_key_removed" }, ct: ct);
        return Ok(new { configured = false });
    }

    /// <summary>Estado actual da configuração — UI mostra "Configurado" ou "Sem key".</summary>
    [HttpGet("anthropic-key/status")]
    public async Task<IActionResult> GetAnthropicKeyStatus(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var tenant = await _tenants.FindByIdAsync(tenantId, ct);
        return Ok(new
        {
            configured = !string.IsNullOrEmpty(tenant?.AnthropicApiKeyCipherText),
            validatedAt = tenant?.AnthropicValidatedAt,
        });
    }

    /// <summary>Resumo de gastos: mês actual + mês passado + lifetime + breakdown por operação.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);
        var lifetimeStart = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var thisMonth = await _repo.GetSummaryAsync(tenantId, monthStart, monthStart.AddMonths(1), ct);
        var prevMonth = await _repo.GetSummaryAsync(tenantId, prevMonthStart, monthStart, ct);
        var lifetime = await _repo.GetSummaryAsync(tenantId, lifetimeStart, now.AddDays(1), ct);
        var recent = await _repo.ListRecentAsync(tenantId, 20, ct);
        var quota = await _quota.CheckAsync(ct);

        return Ok(new
        {
            thisMonth,
            prevMonth,
            lifetime,
            quota = new
            {
                plan = quota.Plan.ToString(),
                used = quota.Used,
                limit = quota.Quota == int.MaxValue ? (int?)null : quota.Quota,
                allowed = quota.Allowed,
                reason = quota.Reason,
            },
            recent = recent.Select(r => new
            {
                createdAt = r.CreatedAt,
                operation = r.Operation,
                model = r.Model,
                inputTokens = r.InputTokens,
                outputTokens = r.OutputTokens,
                costMicrocents = r.CostMicrocents,
                outcome = r.Outcome,
            }),
        });
    }
}
