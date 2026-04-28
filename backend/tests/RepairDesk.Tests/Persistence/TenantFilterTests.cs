using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Persistence;

public class TenantFilterTests
{
    private static AppDbContext NewContext(Guid? tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rd-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    [Fact]
    public void SoftDelete_HidesDeletedRows()
    {
        var ctx = NewContext(null);
        ctx.Tenants.Add(new Tenant { Name = "Alive" });
        ctx.Tenants.Add(new Tenant { Name = "Dead", IsDeleted = true });
        ctx.SaveChanges();

        var visible = ctx.Tenants.Select(t => t.Name).ToList();

        visible.Should().ContainSingle().Which.Should().Be("Alive");
    }

    [Fact]
    public void SoftDelete_OnRemove_KeepsRowAndStampsFlag()
    {
        var ctx = NewContext(null);
        var tenant = new Tenant { Name = "ToDelete" };
        ctx.Tenants.Add(tenant);
        ctx.SaveChanges();

        ctx.Tenants.Remove(tenant);
        ctx.SaveChanges();

        var raw = ctx.Tenants.IgnoreQueryFilters().Single(t => t.Id == tenant.Id);
        raw.IsDeleted.Should().BeTrue();
        raw.UpdatedAt.Should().NotBeNull();
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => TenantId is not null;
    }
}
