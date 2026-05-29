using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 421: persistência do inventário físico (StockTake).</summary>
public interface IStockTakeRepository
{
    Task<StockTake?> GetCurrentOpenAsync(CancellationToken ct = default);
    Task<StockTake?> FindByIdAsync(Guid id, bool includeItems, CancellationToken ct = default);
    Task<IReadOnlyList<StockTake>> ListAsync(int take, CancellationToken ct = default);
    Task AddAsync(StockTake stockTake, CancellationToken ct = default);
    Task AddItemsAsync(IEnumerable<StockTakeItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<Part>> ListActivePartsAsync(CancellationToken ct = default);
    Task<Part?> FindPartByIdAsync(Guid partId, CancellationToken ct = default);
    void AddMovimento(PartMovimento movimento);
    Task SaveAsync(CancellationToken ct = default);
}
