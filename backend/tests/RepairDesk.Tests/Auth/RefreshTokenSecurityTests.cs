using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepairDesk.API.HostedServices;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Auth;

public class RefreshTokenSecurityTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public RefreshTokenSecurityTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Refresh_UpdatesLastUsedAt_WhenTokenIsValidated()
    {
        var client = NewClient();
        var auth = await Login(client, RepairDeskApiFactory.AdminEmail);

        Guid tokenId;
        var oldLastUsed = DateTime.UtcNow.AddDays(-5);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(x => x.UserId == auth.User.Id && x.RevokedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();
            tokenId = token.Id;
            token.LastUsedAt = oldLastUsed;
            await db.SaveChangesAsync();
        }

        var refresh = await client.PostAsync("/api/auth/refresh", content: null);

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshedToken = await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(x => x.Id == tokenId);
            refreshedToken.LastUsedAt.Should().NotBeNull();
            refreshedToken.LastUsedAt.Should().BeAfter(oldLastUsed);
        }
    }

    [Fact]
    public async Task CleanupService_RevokesIdleTokensOlderThanThreshold()
    {
        Guid oldTokenId;
        Guid recentTokenId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Email == RepairDeskApiFactory.AdminEmail);
            var now = DateTime.UtcNow;

            var oldToken = NewToken(user, now.AddDays(-45), now.AddDays(10));
            var recentToken = NewToken(user, now.AddDays(-3), now.AddDays(10));
            db.RefreshTokens.AddRange(oldToken, recentToken);
            await db.SaveChangesAsync();
            oldTokenId = oldToken.Id;
            recentTokenId = recentToken.Id;
        }

        var cleanup = new RefreshTokenCleanupHostedService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RefreshTokenCleanupOptions { RefreshTokenIdleDays = 30 }),
            TimeProvider.System,
            NullLogger<RefreshTokenCleanupHostedService>.Instance);

        var revoked = await cleanup.RunOnceAsync();

        revoked.Should().BeGreaterThan(0);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldToken = await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(x => x.Id == oldTokenId);
            var recentToken = await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(x => x.Id == recentTokenId);

            oldToken.RevokedAt.Should().NotBeNull();
            oldToken.RevokedByIp.Should().Be("idle-timeout");
            recentToken.RevokedAt.Should().BeNull();
        }
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private static async Task<AuthResponse> Login(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private static RefreshToken NewToken(AppUser user, DateTime lastUsedAt, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = user.TenantId,
        UserId = user.Id,
        TokenHash = Guid.NewGuid().ToString("N"),
        CreatedAt = lastUsedAt,
        LastUsedAt = lastUsedAt,
        ExpiresAt = expiresAt,
        CreatedByIp = "test"
    };
}
