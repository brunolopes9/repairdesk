using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IPriceTableRepository
{
    Task<PriceTableEntry?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<PriceTableEntry> Items, int Total)> SearchAsync(
        string? query,
        DeviceCategory? categoria,
        string? marca,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListMarcasAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string marca, string modelo, string servico, Guid? exceptId, CancellationToken ct = default);
    Task AddAsync(PriceTableEntry entry, CancellationToken ct = default);
    void Remove(PriceTableEntry entry);
    Task SaveAsync(CancellationToken ct = default);
}
