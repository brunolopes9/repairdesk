using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 167b: verifica se o tenant ainda tem quota LLM disponível neste mês.
/// Default plans:
///   Free       =  100 chamadas/mês
///   Pro        = 1000 chamadas/mês
///   Enterprise = ilimitado (usa key própria — assume self-paid)
///
/// Tenant.LlmQuotaMonthly pode override o default do plano (ex: custom enterprise =
/// 5000 sem ser unlimited).
/// </summary>
public interface ILlmQuotaService
{
    Task<LlmQuotaCheck> CheckAsync(CancellationToken ct = default);
}

public sealed record LlmQuotaCheck(
    bool Allowed,
    int Used,
    int Quota,
    TenantPlan Plan,
    string? Reason);

public sealed class LlmQuotaService : ILlmQuotaService
{
    private readonly ILlmUsageRepository _usage;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LlmQuotaService> _logger;

    public LlmQuotaService(
        ILlmUsageRepository usage,
        ITenantRepository tenants,
        ITenantContext tenant,
        ILogger<LlmQuotaService> logger)
    {
        _usage = usage;
        _tenants = tenants;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<LlmQuotaCheck> CheckAsync(CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return new LlmQuotaCheck(Allowed: false, 0, 0, TenantPlan.Free, "no_tenant_context");
        }

        var tenant = await _tenants.FindByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return new LlmQuotaCheck(false, 0, 0, TenantPlan.Free, "tenant_not_found");
        }

        var quota = ResolveQuota(tenant.Plan, tenant.LlmQuotaMonthly);

        // Enterprise = quota INFINITA (ou explicit override). Não conta usage.
        if (quota == int.MaxValue)
        {
            return new LlmQuotaCheck(true, 0, int.MaxValue, tenant.Plan, null);
        }

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var summary = await _usage.GetSummaryAsync(tenantId, monthStart, monthStart.AddMonths(1), ct);
        var used = summary.OkCalls; // só conta OK (erros não devem castigar quota)

        if (used >= quota)
        {
            _logger.LogWarning("LLM quota exceeded: tenant={Tid} plan={Plan} used={Used}/{Quota}",
                tenantId, tenant.Plan, used, quota);
            return new LlmQuotaCheck(false, used, quota, tenant.Plan, "quota_exceeded");
        }

        return new LlmQuotaCheck(true, used, quota, tenant.Plan, null);
    }

    /// <summary>Quota mensal final aplicada — override per-tenant prevalece sobre default do plano.</summary>
    internal static int ResolveQuota(TenantPlan plan, int? overrideValue)
    {
        if (overrideValue is { } v && v > 0) return v;
        return plan switch
        {
            TenantPlan.Free => 100,
            TenantPlan.Pro => 1000,
            TenantPlan.Enterprise => int.MaxValue,
            _ => 100,
        };
    }
}
