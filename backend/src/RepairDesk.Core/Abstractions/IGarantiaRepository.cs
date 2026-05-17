using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IGarantiaRepository
{
    Task<Garantia?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Garantia?> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task<Garantia?> FindByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(Garantia g, CancellationToken ct = default);
    void Remove(Garantia g);
    Task SaveAsync(CancellationToken ct = default);
}

public interface IAvaliacaoRepository
{
    Task<Avaliacao?> FindByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<IReadOnlyList<Avaliacao>> ListRecentesAsync(int take, CancellationToken ct = default);
    Task<(double? MediaScore, int Total)> EstatisticasAsync(CancellationToken ct = default);
    /// <summary>Devolve contagem de avaliações por score (1..5). Score sem entradas devolve 0.</summary>
    Task<IReadOnlyDictionary<int, int>> DistribuicaoAsync(CancellationToken ct = default);
    Task AddAsync(Avaliacao a, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
