using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IReparacaoRepository
{
    Task<Reparacao?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Reparacao?> FindByIdWithTimelineAsync(Guid id, CancellationToken ct = default);
    /// <summary>Lookup público (sem auth) por slug. Ignora filtro de tenant para permitir acesso anónimo.</summary>
    Task<Reparacao?> FindByPublicSlugWithTimelineAsync(string slug, CancellationToken ct = default);
    /// <summary>Persists a new repair, assigning the next available <c>Numero</c> for the tenant. Retries on unique-key collisions.</summary>
    Task CreateWithNextNumeroAsync(Reparacao reparacao, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Reparacao> Items, int Total)> SearchAsync(
        string? query,
        RepairStatus? estado,
        Guid? clienteId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    /// <summary>Procura reparações com IMEI normalizado igual (excluindo opcionalmente uma reparação específica).</summary>
    Task<IReadOnlyList<Reparacao>> SearchByImeiAsync(string imeiNormalizado, Guid? excludeId, CancellationToken ct = default);
    /// <summary>Lista reparações pagas (EstadoPagamento.Pago) que ainda não têm fatura emitida.</summary>
    Task<IReadOnlyList<Reparacao>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default);
    /// <summary>Lista todas as reparações do tenant para export (sem paginação).</summary>
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Reparacao>> ExportAllAsync(CancellationToken ct = default);
    void Remove(Reparacao reparacao);
    void AddEstadoLog(ReparacaoEstadoLog log);
    Task SaveAsync(CancellationToken ct = default);
}
