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
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = hash,
            ExpiresAt = _clock.GetUtcNow().UtcDateTime.AddDays(_opt.RefreshTokenDays),
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
        return token?.IsActive == true ? token : null;
    }

    public async Task<(string PlaintextToken, RefreshToken Stored)> RotateAsync(RefreshToken existing, AppUser user, string? ip, CancellationToken ct = default)
    {
        var (plaintext, replacement) = await IssueAsync(user, ip, ct);
        existing.RevokedAt = _clock.GetUtcNow().UtcDateTime;
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
