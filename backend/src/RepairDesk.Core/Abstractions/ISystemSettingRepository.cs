using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> FindAsync(string key, CancellationToken ct = default);
    Task AddAsync(SystemSetting setting, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
