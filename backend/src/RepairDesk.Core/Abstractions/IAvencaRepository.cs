using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 546: persistência das avenças (faturação recorrente).</summary>
public interface IAvencaRepository
{
    Task<Avenca?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Lista do tenant (Cliente incluído), opcionalmente filtrada por cliente. Ordenada por ProximaEmissao.</summary>
    Task<IReadOnlyList<Avenca>> ListAsync(Guid? clienteId, CancellationToken ct = default);
    Task AddAsync(Avenca avenca, CancellationToken ct = default);
    void Remove(Avenca avenca);
    Task SaveAsync(CancellationToken ct = default);
}
