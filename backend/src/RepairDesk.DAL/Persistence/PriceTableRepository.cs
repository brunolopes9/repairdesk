using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class PriceTableRepository : IPriceTableRepository
{
    private readonly AppDbContext _db;
    public PriceTableRepository(AppDbContext db) => _db = db;

    public Task<PriceTableEntry?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.PriceTableEntries.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<PriceTableEntry> Items, int Total)> SearchAsync(
        string? query, DeviceCategory? categoria, string? marca, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.PriceTableEntries.AsNoTracking().AsQueryable();
        if (categoria is not null) q = q.Where(p => p.Categoria == categoria.Value);
        if (!string.IsNullOrWhiteSpace(marca)) q = q.Where(p => p.Marca == marca);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(p =>
                EF.Functions.Like(p.Marca, like) ||
                EF.Functions.Like(p.Modelo, like) ||
                EF.Functions.Like(p.Servico, like) ||
                (p.Notas != null && EF.Functions.Like(p.Notas, like)));
        }
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(p => p.Marca).ThenBy(p => p.Modelo).ThenBy(p => p.Servico)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<string>> ListMarcasAsync(CancellationToken ct = default)
        => await _db.PriceTableEntries
            .AsNoTracking()
            .Select(p => p.Marca)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(string marca, string modelo, string servico, Guid? exceptId, CancellationToken ct = default)
        => _db.PriceTableEntries.AnyAsync(p =>
            p.Marca == marca &&
            p.Modelo == modelo &&
            p.Servico == servico &&
            (exceptId == null || p.Id != exceptId), ct);

    public Task AddAsync(PriceTableEntry entry, CancellationToken ct = default)
        => _db.PriceTableEntries.AddAsync(entry, ct).AsTask();

    public void Remove(PriceTableEntry entry) => _db.PriceTableEntries.Remove(entry);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
