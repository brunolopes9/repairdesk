using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IRepairRequestRepository
{
    Task<IReadOnlyList<RepairRequest>> ListAsync(RepairRequestEstado? estado, CancellationToken ct = default);
    Task<int> CountPendentesAsync(CancellationToken ct = default);
    Task<RepairRequest?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RepairRequest req, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Sprint 354: contagem de pedidos do mesmo IP nas últimas 24h (anti-abuso).</summary>
    Task<int> CountRecentByIpAsync(Guid tenantId, string sourceIp, TimeSpan window, CancellationToken ct = default);
}
