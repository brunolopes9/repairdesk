using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.TenantPreferences;

public class WhatsAppNotificationLogRepositoryTests
{
    [Fact]
    public async Task ExistsAsync_ReturnsTrue_AfterClickLogged()
    {
        var tenantId = Guid.NewGuid();
        var repairId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var repo = new WhatsAppNotificationLogRepository(db);

        await repo.AddAsync(new WhatsAppNotificationLog
        {
            TenantId = tenantId,
            EntityId = repairId,
            EntityType = "Reparacao",
            TemplateKey = "Pronto",
            Estado = RepairStatus.Pronto,
            Phone = "+351910000000",
        });
        await repo.SaveAsync();

        (await repo.ExistsAsync(repairId, "Pronto")).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_IsScopedByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var repairId = Guid.NewGuid();
        var databaseName = $"wa-log-{Guid.NewGuid():N}";

        await using (var dbA = NewDb(tenantA, databaseName))
        {
            var repoA = new WhatsAppNotificationLogRepository(dbA);
            await repoA.AddAsync(new WhatsAppNotificationLog
            {
                TenantId = tenantA,
                EntityId = repairId,
                EntityType = "Reparacao",
                TemplateKey = "Pronto",
            });
            await repoA.SaveAsync();
        }

        await using var dbB = NewDb(tenantB, databaseName);
        var repoB = new WhatsAppNotificationLogRepository(dbB);

        (await repoB.ExistsAsync(repairId, "Pronto")).Should().BeFalse();
    }

    private static AppDbContext NewDb(Guid tenantId, string? name = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"wa-log-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }
}
