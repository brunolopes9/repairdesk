using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IReparacaoFotoRepository
{
    Task<IReadOnlyList<ReparacaoFoto>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<ReparacaoFoto?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Procura por reparações de uma tenant específica (cruzando tenant via Reparacao). Usado pelo endpoint público (sem filter de tenant aplicado).</summary>
    Task<IReadOnlyList<ReparacaoFoto>> ListPublicByReparacaoIdAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(ReparacaoFoto foto, CancellationToken ct = default);
    void Remove(ReparacaoFoto foto);
    Task SaveAsync(CancellationToken ct = default);
}
