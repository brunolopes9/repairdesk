using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IVendaFotoRepository
{
    Task<IReadOnlyList<VendaFoto>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task<VendaFoto?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Procura por reparações de uma tenant específica (cruzando tenant via Reparacao). Usado pelo endpoint público (sem filter de tenant aplicado).</summary>
    Task<IReadOnlyList<VendaFoto>> ListPublicByVendaIdAsync(Guid vendaId, CancellationToken ct = default);
    Task AddAsync(VendaFoto foto, CancellationToken ct = default);
    void Remove(VendaFoto foto);
    Task SaveAsync(CancellationToken ct = default);
}
