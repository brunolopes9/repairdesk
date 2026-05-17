using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Persistence;

public class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AppDbContext NewContext(string dbName, Guid? tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    [Fact]
    public void RefreshTokens_ScopedToCurrentTenant_AreInvisibleAcrossTenants()
    {
        var dbName = $"rd-tenant-{Guid.NewGuid():N}";
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // seed both tenants without any tenant context
        using (var seed = NewContext(dbName, tenantId: null))
        {
            seed.RefreshTokens.Add(new RefreshToken
            {
                TenantId = TenantA, UserId = userA,
                TokenHash = "hash-a", ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            seed.RefreshTokens.Add(new RefreshToken
            {
                TenantId = TenantB, UserId = userB,
                TokenHash = "hash-b", ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            seed.SaveChanges();
        }

        using (var asA = NewContext(dbName, TenantA))
        {
            var visible = asA.RefreshTokens.Select(t => t.TokenHash).ToList();
            visible.Should().ContainSingle().Which.Should().Be("hash-a");
        }

        using (var asB = NewContext(dbName, TenantB))
        {
            var visible = asB.RefreshTokens.Select(t => t.TokenHash).ToList();
            visible.Should().ContainSingle().Which.Should().Be("hash-b");
        }
    }

    [Fact]
    public void EnforceTenantOnInsert_StampsCurrentTenantId()
    {
        var dbName = $"rd-tenant-{Guid.NewGuid():N}";
        using var ctx = NewContext(dbName, TenantA);

        var token = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = "x",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
            // intentionally no TenantId — should be stamped
        };
        ctx.RefreshTokens.Add(token);
        ctx.SaveChanges();

        token.TenantId.Should().Be(TenantA);
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => TenantId is not null;
    }
}
