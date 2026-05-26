using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class PartKitRepository : IPartKitRepository
{
    private readonly AppDbContext _db;
    public PartKitRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PartKit>> ListAsync(CancellationToken ct = default) =>
        await _db.PartKits
            .Include(k => k.Items).ThenInclude(i => i.Part)
            .OrderBy(k => k.Nome)
            .ToListAsync(ct);

    public Task<PartKit?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PartKits
            .Include(k => k.Items).ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(k => k.Id == id, ct);

    public Task<PartKit?> FindByNomeAsync(string nome, CancellationToken ct = default)
    {
        var normalizado = nome.Trim().ToLowerInvariant();
        return _db.PartKits.FirstOrDefaultAsync(k => k.Nome.ToLower() == normalizado, ct);
    }

    public async Task AddAsync(PartKit kit, CancellationToken ct = default)
    {
        await _db.PartKits.AddAsync(kit, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(PartKit kit, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(PartKit kit, CancellationToken ct = default)
    {
        _db.PartKits.Remove(kit);
        await _db.SaveChangesAsync(ct);
    }
}
