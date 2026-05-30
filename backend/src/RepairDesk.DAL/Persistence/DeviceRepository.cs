using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 461: implementação EF do <see cref="IDeviceRepository"/>.</summary>
public class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _db;
    public DeviceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Device>> ListByClienteAsync(Guid clienteId, bool incluirArquivados, CancellationToken ct = default)
    {
        var q = _db.Devices.AsNoTracking().Where(d => d.ClienteId == clienteId);
        if (!incluirArquivados) q = q.Where(d => !d.Arquivado);
        return await q.OrderBy(d => d.Arquivado).ThenByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public Task<Device?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Device?> FindByImeiAsync(string imei, CancellationToken ct = default)
    {
        var norm = imei.Trim();
        return _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Imei == norm, ct);
    }

    public Task<bool> ExistsImeiAsync(string imei, Guid? excludeId, CancellationToken ct = default)
    {
        var norm = imei.Trim();
        var q = _db.Devices.AsNoTracking().Where(d => d.Imei == norm);
        if (excludeId is { } x) q = q.Where(d => d.Id != x);
        return q.AnyAsync(ct);
    }

    public async Task AddAsync(Device device, CancellationToken ct = default)
        => await _db.Devices.AddAsync(device, ct);

    public void Remove(Device device) => _db.Devices.Remove(device);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
