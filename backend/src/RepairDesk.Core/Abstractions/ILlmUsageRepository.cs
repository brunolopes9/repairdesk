using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ILlmUsageRepository
{
    Task AddAsync(LlmUsage usage, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Agrega uso de um tenant no período. Útil para dashboard + quota check.</summary>
    Task<LlmUsageSummary> GetSummaryAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Lista últimas N chamadas para debug/audit.</summary>
    Task<IReadOnlyList<LlmUsage>> ListRecentAsync(Guid tenantId, int take, CancellationToken ct = default);
}

public sealed record LlmUsageSummary(
    int TotalCalls,
    int OkCalls,
    int ErrorCalls,
    int TotalInputTokens,
    int TotalOutputTokens,
    int TotalCostMicrocents,
    IReadOnlyList<LlmUsageBreakdown> ByOperation);

public sealed record LlmUsageBreakdown(
    string Operation,
    int Calls,
    int CostMicrocents);
