using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly AppDbContext _db;

    public SystemSettingRepository(AppDbContext db) => _db = db;

    public Task<SystemSetting?> FindAsync(string key, CancellationToken ct = default)
        => _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, ct);

    public async Task AddAsync(SystemSetting setting, CancellationToken ct = default)
        => await _db.SystemSettings.AddAsync(setting, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
