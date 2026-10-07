using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 551: persistência das assinaturas de entrada/entrega por reparação.</summary>
public interface IVendaAssinaturaRepository
{
    Task<VendaAssinatura?> FindAsync(Guid vendaId, AssinaturaTipo tipo, CancellationToken ct = default);
    Task<IReadOnlyList<VendaAssinatura>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task AddAsync(VendaAssinatura assinatura, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
