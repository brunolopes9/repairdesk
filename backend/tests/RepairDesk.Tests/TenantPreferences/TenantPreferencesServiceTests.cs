using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.TenantPreferences;
using TenantPreferencesEntity = RepairDesk.Core.Entities.TenantPreferences;

namespace RepairDesk.Tests.TenantPreferences;

public class TenantPreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_WhenMissing_CreatesDefaults()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var service = NewService(db, tenantId);

        var prefs = await service.GetAsync();

        prefs.Communication.WhatsAppEnabled.Should().BeTrue();
        prefs.Communication.StaleDaysThreshold.Should().Be(7);
        prefs.Repairs.EntregarMarcaPago.Should().Be(EntregarMarcaPagoMode.Sim);
        (await db.TenantPreferences.CountAsync(x => x.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAndInvalidatesCache()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var service = NewService(db, tenantId);
        var prefs = await service.GetAsync();

        var updated = prefs with
        {
            Communication = prefs.Communication with { WhatsAppEnabled = false, StaleDaysThreshold = 12 },
        };
        await service.UpdateAsync(updated);

        var loaded = await service.GetAsync();
        loaded.Communication.WhatsAppEnabled.Should().BeFalse();
        loaded.Communication.StaleDaysThreshold.Should().Be(12);
        (await db.TenantPreferences.SingleAsync(x => x.TenantId == tenantId)).PreferencesJson.Should().Contain("\"staleDaysThreshold\":12");
    }

    [Fact]
    public async Task GetAsync_InvalidJson_ResetsToDefaults()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.TenantPreferences.Add(new TenantPreferencesEntity
        {
            TenantId = tenantId,
            Version = 1,
            PreferencesJson = "{invalid-json",
        });
        await db.SaveChangesAsync();
        var service = NewService(db, tenantId);

        var prefs = await service.GetAsync();

        prefs.Communication.WhatsAppEnabled.Should().BeTrue();
        (await db.TenantPreferences.SingleAsync()).PreferencesJson.Should().Contain("\"communication\"");
    }

    [Fact]
    public async Task GetForTenantAsync_IgnoresCurrentTenantFilter_ForPublicPortal()
    {
        var currentTenant = Guid.NewGuid();
        var targetTenant = Guid.NewGuid();
        await using var db = NewDb(currentTenant);
        db.TenantPreferences.Add(new TenantPreferencesEntity
        {
            TenantId = targetTenant,
            Version = 1,
            PreferencesJson = "{\"communication\":{\"whatsAppEnabled\":false,\"templatesByState\":{},\"repeatMode\":0,\"staleDaysThreshold\":3,\"push\":{\"enabled\":true,\"estadosPermitidos\":[\"Recebido\"]}},\"portal\":{\"mostrarFotos\":true,\"mostrarDiagnostico\":true,\"mostrarOrcamento\":true,\"mostrarGarantia\":true,\"mostrarTimeline\":true,\"mostrarAvaliacao\":true,\"permitirAprovarOrcamento\":true,\"googleReviewMinScore\":4,\"googleReviewUrl\":null},\"repairs\":{\"entregarMarcaPago\":0,\"garantiaAutomatica\":0},\"sales\":{\"defaultMetodoPagamento\":\"MBWay\",\"defaultCondicaoArtigo\":0,\"emitirFatura\":1,\"vendaGarantia\":0}}",
        });
        await db.SaveChangesAsync();
        var service = NewService(db, currentTenant);

        var prefs = await service.GetForTenantAsync(targetTenant);

        prefs.Communication.StaleDaysThreshold.Should().Be(3);
    }

    private static AppDbContext NewDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant-prefs-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private static TenantPreferencesService NewService(AppDbContext db, Guid tenantId)
        => new(new TestTenantContext(tenantId), new TenantPreferencesRepository(db));

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }
}
