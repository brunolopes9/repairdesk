using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IVendaRepository
{
    Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default);
    Task CreateWithNextNumeroAsync(Venda venda, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Venda> Items, int Total)> SearchAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<int> SumPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<TopVendaItemRow>> TopItemsByRevenueAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public sealed record TopVendaItemRow(
    Guid? PartId,
    string Descricao,
    int Quantidade,
    int TotalCents);
