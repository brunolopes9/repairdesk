using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ITokenService
{
    /// <param name="tenantId">Sprint 606: empresa ativa (null = a do utilizador).</param>
    string IssueAccessToken(AppUser user, IEnumerable<string> roles, Guid? tenantId = null);
    DateTime AccessTokenExpiry { get; }
}

public interface IRefreshTokenService
{
    Task<(string PlaintextToken, RefreshToken Stored)> IssueAsync(AppUser user, string? ip, CancellationToken ct = default);
    /// <summary>Sprint 606: sessão numa empresa concreta (a do utilizador ou uma a que tem acesso).</summary>
    Task<(string PlaintextToken, RefreshToken Stored)> IssueParaEmpresaAsync(AppUser user, Guid tenantId, string? ip, CancellationToken ct = default);
    Task<RefreshToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default);
    Task<(string PlaintextToken, RefreshToken Stored)> RotateAsync(RefreshToken existing, AppUser user, string? ip, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, string? ip, CancellationToken ct = default);
    Task<int> RevokeAllForUserAsync(Guid userId, string? ip, CancellationToken ct = default);
    Task<int> RevokeIdleAsync(DateTime cutoffUtc, string? ip, CancellationToken ct = default);
}
