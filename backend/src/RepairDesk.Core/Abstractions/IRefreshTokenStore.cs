using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<int> RevokeAllForUserAsync(Guid userId, string? ip, CancellationToken ct);
    Task<int> RevokeIdleAsync(DateTime cutoffUtc, DateTime revokedAtUtc, string? ip, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
