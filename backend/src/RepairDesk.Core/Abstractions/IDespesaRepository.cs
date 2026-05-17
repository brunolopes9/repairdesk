using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IDespesaRepository
{
    Task<Despesa?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> SumByTrabalhoAsync(Guid trabalhoId, CancellationToken ct = default);
    Task<int> SumByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query,
        DespesaCategoria? categoria,
        DateTime? from,
        DateTime? to,
        Guid? trabalhoId,
        Guid? reparacaoId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Despesa despesa, CancellationToken ct = default);
    void Remove(Despesa despesa);
    Task SaveAsync(CancellationToken ct = default);
}
