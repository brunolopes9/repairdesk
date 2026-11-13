using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 656: encomendas da loja online.</summary>
public interface IEncomendaLojaRepository
{
    Task<IReadOnlyList<EncomendaLoja>> ListAsync(CancellationToken ct = default);
    Task<EncomendaLoja?> FindAsync(Guid id, CancellationToken ct = default);
    Task<EncomendaLoja?> FindByReferenciaAsync(string referencia, CancellationToken ct = default);
    /// <summary>Sem filtro de empresa — o webhook do Stripe chega sem sessão; a empresa vem da própria encomenda.</summary>
    Task<EncomendaLoja?> FindByStripeSessionGlobalAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ReferenciaExisteAsync(string referencia, CancellationToken ct = default);
    Task AddAsync(EncomendaLoja encomenda, CancellationToken ct = default);
    /// <summary>Grava e atribui o nº visível às novas (com nova tentativa se houver colisão).</summary>
    Task SaveAsync(CancellationToken ct = default);
}
