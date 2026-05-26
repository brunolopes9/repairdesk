using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ReparacaoTimeEntryRepository : IReparacaoTimeEntryRepository
{
    private readonly AppDbContext _db;

    public ReparacaoTimeEntryRepository(AppDbContext db) => _db = db;

    public Task<ReparacaoTimeEntry?> FindActiveForUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.ReparacaoTimeEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.EndedAt == null, ct);

    public Task<ReparacaoTimeEntry?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ReparacaoTimeEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<ReparacaoTimeEntry>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default) =>
        await _db.ReparacaoTimeEntries
            .Where(e => e.ReparacaoId == reparacaoId)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ReparacaoTimeEntry entry, CancellationToken ct = default)
    {
        await _db.ReparacaoTimeEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(ReparacaoTimeEntry entry, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(ReparacaoTimeEntry entry, CancellationToken ct = default)
    {
        _db.ReparacaoTimeEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> StopAllActiveForReparacaoAsync(Guid reparacaoId, DateTime endedAt, CancellationToken ct = default)
    {
        var active = await _db.ReparacaoTimeEntries
            .Where(e => e.ReparacaoId == reparacaoId && e.EndedAt == null)
            .ToListAsync(ct);
        if (active.Count == 0) return 0;
        foreach (var e in active) e.EndedAt = endedAt;
        await _db.SaveChangesAsync(ct);
        return active.Count;
    }

    public async Task<int> SumMinutesByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rows = await _db.ReparacaoTimeEntries
            .Where(e => e.ReparacaoId == reparacaoId && e.EndedAt != null)
            .Select(e => new { e.StartedAt, e.EndedAt })
            .ToListAsync(ct);
        return rows.Sum(r => (int)Math.Round((r.EndedAt!.Value - r.StartedAt).TotalMinutes));
    }

    public async Task<IReadOnlyList<TimeStatsRow>> StatsByUserAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await _db.ReparacaoTimeEntries
            .Where(e => e.EndedAt != null && e.StartedAt >= from && e.StartedAt < to)
            .Select(e => new { e.UserId, e.ReparacaoId, e.StartedAt, e.EndedAt })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.UserId)
            .Select(g => new TimeStatsRow(
                UserId: g.Key,
                TotalMinutos: g.Sum(r => (int)Math.Round((r.EndedAt!.Value - r.StartedAt).TotalMinutes)),
                Sessoes: g.Count(),
                Reparacoes: g.Select(r => r.ReparacaoId).Distinct().Count()))
            .OrderByDescending(s => s.TotalMinutos)
            .ToList();
    }
}
