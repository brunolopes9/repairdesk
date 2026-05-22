using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class LlmUsageRepository : ILlmUsageRepository
{
    private readonly AppDbContext _db;

    public LlmUsageRepository(AppDbContext db) => _db = db;

    public Task AddAsync(LlmUsage usage, CancellationToken ct = default)
        => _db.LlmUsage.AddAsync(usage, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<LlmUsageSummary> GetSummaryAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await _db.LlmUsage
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
            .ToListAsync(ct);

        var breakdown = rows
            .GroupBy(r => r.Operation)
            .Select(g => new LlmUsageBreakdown(g.Key, g.Count(), g.Sum(r => r.CostMicrocents)))
            .OrderByDescending(b => b.CostMicrocents)
            .ToList();

        return new LlmUsageSummary(
            TotalCalls: rows.Count,
            OkCalls: rows.Count(r => r.Outcome == "ok"),
            ErrorCalls: rows.Count(r => r.Outcome != "ok"),
            TotalInputTokens: rows.Sum(r => r.InputTokens),
            TotalOutputTokens: rows.Sum(r => r.OutputTokens),
            TotalCostMicrocents: rows.Sum(r => r.CostMicrocents),
            ByOperation: breakdown);
    }

    public async Task<IReadOnlyList<LlmUsage>> ListRecentAsync(Guid tenantId, int take, CancellationToken ct = default)
    {
        return await _db.LlmUsage
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }
}
