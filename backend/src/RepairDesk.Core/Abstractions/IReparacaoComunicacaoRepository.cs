using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 452: persistência de comunicações por reparação.</summary>
public interface IReparacaoComunicacaoRepository
{
    Task<IReadOnlyList<ReparacaoComunicacao>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    /// <summary>Sprint 453: histórico cliente — todas as comunicações de todas as reparações deste cliente.</summary>
    Task<IReadOnlyList<ReparacaoComunicacao>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default);
    Task<ReparacaoComunicacao?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> CountByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(ReparacaoComunicacao entry, CancellationToken ct = default);
    void Remove(ReparacaoComunicacao entry);
    Task SaveAsync(CancellationToken ct = default);
}
