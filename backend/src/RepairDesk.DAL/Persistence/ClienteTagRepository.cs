using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ClienteTagRepository : IClienteTagRepository
{
    private readonly AppDbContext _db;

    public ClienteTagRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClienteTag>> ListAsync(CancellationToken ct = default) =>
        await _db.ClienteTags.OrderBy(t => t.Nome).ToListAsync(ct);

    public Task<ClienteTag?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ClienteTags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ClienteTag?> FindByNomeAsync(string nome, CancellationToken ct = default)
    {
        var normalizado = nome.Trim().ToLowerInvariant();
        return _db.ClienteTags.FirstOrDefaultAsync(t => t.Nome.ToLower() == normalizado, ct);
    }

    public async Task AddAsync(ClienteTag tag, CancellationToken ct = default)
    {
        await _db.ClienteTags.AddAsync(tag, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(ClienteTag tag, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(ClienteTag tag, CancellationToken ct = default)
    {
        _db.ClienteTags.Remove(tag);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetTagsForClienteAsync(Guid clienteId, IReadOnlyList<Guid> tagIds, CancellationToken ct = default)
    {
        var allowedTagIds = await _db.ClienteTags
            .Where(t => tagIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        var desired = allowedTagIds.Distinct().ToHashSet();
        var existing = await _db.ClienteTagAssignments
            .Where(a => a.ClienteId == clienteId)
            .ToListAsync(ct);
        var current = existing.Select(a => a.ClienteTagId).ToHashSet();

        var toRemove = existing.Where(a => !desired.Contains(a.ClienteTagId)).ToList();
        var toAdd = desired.Except(current)
            .Select(tagId => new ClienteTagAssignment { Id = Guid.NewGuid(), ClienteId = clienteId, ClienteTagId = tagId });

        _db.ClienteTagAssignments.RemoveRange(toRemove);
        await _db.ClienteTagAssignments.AddRangeAsync(toAdd, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClienteTag>> ListByClienteAsync(Guid clienteId, CancellationToken ct = default) =>
        await _db.ClienteTagAssignments
            .Where(a => a.ClienteId == clienteId)
            .Select(a => a.ClienteTag!)
            .OrderBy(t => t.Nome)
            .ToListAsync(ct);
}
