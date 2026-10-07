using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IDespesaRepository
{
    Task<Despesa?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query,
        DespesaCategoria? categoria,
        IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports,
        bool excludeSupplierInvoiceImports,
        DateTime? from,
        DateTime? to,
        bool? isRecorrente,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Despesa despesa, CancellationToken ct = default);
    void Remove(Despesa despesa);
    Task SaveAsync(CancellationToken ct = default);
}
