using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenStore _store;
    private readonly JwtOptions _opt;
    private readonly TimeProvider _clock;

    public RefreshTokenService(IRefreshTokenStore store, IOptions<JwtOptions> opt, TimeProvider clock)
    {
        _store = store;
        _opt = opt.Value;
        _clock = clock;
    }

    public async Task<(string PlaintextToken, RefreshToken Stored)> IssueAsync(AppUser user, string? ip, CancellationToken ct = default)
    {
        var (plaintext, hash) = GenerateToken();
        var now = _clock.GetUtcNow().UtcDateTime;
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = hash,
            ExpiresAt = now.AddDays(_opt.RefreshTokenDays),
            LastUsedAt = now,
            CreatedByIp = ip,
        };
        await _store.AddAsync(entity, ct);
        await _store.SaveAsync(ct);
        return (plaintext, entity);
    }

    public async Task<RefreshToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken)) return null;
        var hash = HashToken(plaintextToken);
        var token = await _store.FindByHashAsync(hash, ct);
        if (token?.IsActive != true) return null;

        token.LastUsedAt = _clock.GetUtcNow().UtcDateTime;
        await _store.SaveAsync(ct);
        return token;
    }

    public async Task<(string PlaintextToken, RefreshToken Stored)> RotateAsync(RefreshToken existing, AppUser user, string? ip, CancellationToken ct = default)
    {
        var (plaintext, replacement) = await IssueAsync(user, ip, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        existing.LastUsedAt = now;
        existing.RevokedAt = now;
        existing.RevokedByIp = ip;
        existing.ReplacedByTokenId = replacement.Id;
        await _store.SaveAsync(ct);
        return (plaintext, replacement);
    }

    public async Task RevokeAsync(RefreshToken token, string? ip, CancellationToken ct = default)
    {
        if (token.RevokedAt is not null) return;
        token.RevokedAt = _clock.GetUtcNow().UtcDateTime;
        token.RevokedByIp = ip;
        await _store.SaveAsync(ct);
    }

    public Task<int> RevokeAllForUserAsync(Guid userId, string? ip, CancellationToken ct = default)
        => _store.RevokeAllForUserAsync(userId, ip, ct);

    public Task<int> RevokeIdleAsync(DateTime cutoffUtc, string? ip, CancellationToken ct = default)
        => _store.RevokeIdleAsync(cutoffUtc, _clock.GetUtcNow().UtcDateTime, ip, ct);

    private static (string Plaintext, string Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plaintext = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (plaintext, HashToken(plaintext));
    }

    private static string HashToken(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes);
    }
}
