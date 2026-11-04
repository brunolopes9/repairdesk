using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 595: ficha de entrada (ordem de reparação) de uma venda.</summary>
public interface IVendaFichaRepository
{
    Task<VendaFicha?> FindByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task AddAsync(VendaFicha ficha, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
