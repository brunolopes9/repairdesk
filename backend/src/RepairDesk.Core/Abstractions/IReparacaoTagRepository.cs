using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 346: persistência de tags de reparação.</summary>
public interface IReparacaoTagRepository
{
    Task<IReadOnlyList<ReparacaoTag>> ListAsync(CancellationToken ct = default);
    Task<ReparacaoTag?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoTag?> FindByNomeAsync(string nome, CancellationToken ct = default);
    Task AddAsync(ReparacaoTag tag, CancellationToken ct = default);
    Task UpdateAsync(ReparacaoTag tag, CancellationToken ct = default);
    Task DeleteAsync(ReparacaoTag tag, CancellationToken ct = default);

    /// <summary>Substitui o conjunto completo de tags de uma reparação.</summary>
    Task SetTagsForReparacaoAsync(Guid reparacaoId, IReadOnlyList<Guid> tagIds, CancellationToken ct = default);

    /// <summary>Tags atribuídas a uma reparação.</summary>
    Task<IReadOnlyList<ReparacaoTag>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
}
