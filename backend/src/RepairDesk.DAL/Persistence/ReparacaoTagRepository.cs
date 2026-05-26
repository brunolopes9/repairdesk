using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ReparacaoTagRepository : IReparacaoTagRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ReparacaoTagRepository(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ReparacaoTag>> ListAsync(CancellationToken ct = default) =>
        await _db.ReparacaoTags.OrderBy(t => t.Nome).ToListAsync(ct);

    public Task<ReparacaoTag?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ReparacaoTags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ReparacaoTag?> FindByNomeAsync(string nome, CancellationToken ct = default)
    {
        var normalizado = nome.Trim().ToLowerInvariant();
        return _db.ReparacaoTags.FirstOrDefaultAsync(t => t.Nome.ToLower() == normalizado, ct);
    }

    public async Task AddAsync(ReparacaoTag tag, CancellationToken ct = default)
    {
        await _db.ReparacaoTags.AddAsync(tag, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(ReparacaoTag tag, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(ReparacaoTag tag, CancellationToken ct = default)
    {
        _db.ReparacaoTags.Remove(tag);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetTagsForReparacaoAsync(Guid reparacaoId, IReadOnlyList<Guid> tagIds, CancellationToken ct = default)
    {
        var existing = await _db.ReparacaoTagAssignments
            .Where(a => a.ReparacaoId == reparacaoId)
            .ToListAsync(ct);

        var desired = tagIds.Distinct().ToHashSet();
        var current = existing.Select(a => a.ReparacaoTagId).ToHashSet();

        var toRemove = existing.Where(a => !desired.Contains(a.ReparacaoTagId)).ToList();
        var toAdd = desired.Except(current)
            .Select(tagId => new ReparacaoTagAssignment { Id = Guid.NewGuid(), ReparacaoId = reparacaoId, ReparacaoTagId = tagId });

        _db.ReparacaoTagAssignments.RemoveRange(toRemove);
        await _db.ReparacaoTagAssignments.AddRangeAsync(toAdd, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReparacaoTag>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default) =>
        await _db.ReparacaoTagAssignments
            .Where(a => a.ReparacaoId == reparacaoId)
            .Select(a => a.ReparacaoTag!)
            .OrderBy(t => t.Nome)
            .ToListAsync(ct);
}
