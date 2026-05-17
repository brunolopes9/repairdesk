using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ITokenService
{
    string IssueAccessToken(AppUser user, IEnumerable<string> roles);
    DateTime AccessTokenExpiry { get; }
}

public interface IRefreshTokenService
{
    Task<(string PlaintextToken, RefreshToken Stored)> IssueAsync(AppUser user, string? ip, CancellationToken ct = default);
    Task<RefreshToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default);
    Task<(string PlaintextToken, RefreshToken Stored)> RotateAsync(RefreshToken existing, AppUser user, string? ip, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, string? ip, CancellationToken ct = default);
}
