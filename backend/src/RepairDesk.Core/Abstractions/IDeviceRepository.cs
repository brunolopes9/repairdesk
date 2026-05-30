using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 461: persistência de Devices (asset registry).</summary>
public interface IDeviceRepository
{
    Task<IReadOnlyList<Device>> ListByClienteAsync(Guid clienteId, bool incluirArquivados, CancellationToken ct = default);
    Task<Device?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Device?> FindByImeiAsync(string imei, CancellationToken ct = default);
    Task<bool> ExistsImeiAsync(string imei, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
    void Remove(Device device);
    Task SaveAsync(CancellationToken ct = default);
}
