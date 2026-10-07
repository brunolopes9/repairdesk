using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 452: persistência de comunicações por reparação.</summary>
public interface IVendaComunicacaoRepository
{
    Task<IReadOnlyList<VendaComunicacao>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default);
    /// <summary>Sprint 453: histórico cliente — todas as comunicações de todas as reparações deste cliente.</summary>
    Task<IReadOnlyList<VendaComunicacao>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default);
    Task<VendaComunicacao?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> CountByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task AddAsync(VendaComunicacao entry, CancellationToken ct = default);
    void Remove(VendaComunicacao entry);
    Task SaveAsync(CancellationToken ct = default);
}
