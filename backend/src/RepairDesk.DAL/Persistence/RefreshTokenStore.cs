using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _db;

    public RefreshTokenStore(AppDbContext db) => _db = db;

    public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct)
        => _db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct)
    {
        await _db.RefreshTokens.AddAsync(token, ct);
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, string? ip, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ip;
        }

        await _db.SaveChangesAsync(ct);
        return tokens.Count;
    }

    public async Task<int> RevokeIdleAsync(DateTime cutoffUtc, DateTime revokedAtUtc, string? ip, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(x => x.RevokedAt == null && (x.LastUsedAt ?? x.CreatedAt) < cutoffUtc)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAtUtc;
            token.RevokedByIp = ip;
        }

        await _db.SaveChangesAsync(ct);
        return tokens.Count;
    }

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
