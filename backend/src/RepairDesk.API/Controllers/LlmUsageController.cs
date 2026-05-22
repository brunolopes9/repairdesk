using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Controllers;

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

    public LlmUsageController(ILlmUsageRepository repo, ITenantContext tenant, ILlmQuotaService quota)
    {
        _repo = repo;
        _tenant = tenant;
        _quota = quota;
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
