using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
