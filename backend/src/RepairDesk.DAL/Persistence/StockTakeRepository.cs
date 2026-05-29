using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 421: implementação EF do <see cref="IStockTakeRepository"/>.</summary>
public class StockTakeRepository : IStockTakeRepository
{
    private readonly AppDbContext _db;

    public StockTakeRepository(AppDbContext db) => _db = db;

    public Task<StockTake?> GetCurrentOpenAsync(CancellationToken ct = default)
        => _db.StockTakes.FirstOrDefaultAsync(s => s.Status == StockTakeStatus.Aberto, ct);

    public Task<StockTake?> FindByIdAsync(Guid id, bool includeItems, CancellationToken ct = default)
    {
        var q = _db.StockTakes.AsQueryable();
        if (includeItems) q = q.Include(s => s.Items).ThenInclude(i => i.Part);
        return q.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<StockTake>> ListAsync(int take, CancellationToken ct = default)
        => await _db.StockTakes.AsNoTracking()
            .OrderByDescending(s => s.OpenedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(StockTake stockTake, CancellationToken ct = default)
        => await _db.StockTakes.AddAsync(stockTake, ct);

    public async Task AddItemsAsync(IEnumerable<StockTakeItem> items, CancellationToken ct = default)
        => await _db.StockTakeItems.AddRangeAsync(items, ct);

    public async Task<IReadOnlyList<Part>> ListActivePartsAsync(CancellationToken ct = default)
        => await _db.Parts.Where(p => p.Activo).OrderBy(p => p.Nome).ToListAsync(ct);

    public Task<Part?> FindPartByIdAsync(Guid partId, CancellationToken ct = default)
        => _db.Parts.FirstOrDefaultAsync(p => p.Id == partId, ct);

    public void AddMovimento(PartMovimento movimento) => _db.PartMovimentos.Add(movimento);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
