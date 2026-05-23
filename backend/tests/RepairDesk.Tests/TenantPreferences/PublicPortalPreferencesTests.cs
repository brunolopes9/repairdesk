using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.PublicPortal;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.Tests.TenantPreferences;

public class PublicPortalPreferencesTests
{
    [Fact]
    public async Task GetBySlugAsync_MostrarFotosFalse_ReturnsNoPhotos()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        db.ReparacaoFotos.Add(new ReparacaoFoto
        {
            TenantId = tenantId,
            ReparacaoId = rep.Id,
            StorageKey = "photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Size = 100,
            VisivelNoPortal = true,
        });
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarFotos = false } };
        var service = NewService(db, tenantId, prefs);

        var dto = await service.GetBySlugAsync(rep.PublicSlug!);

        dto.Fotos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBySlugAsync_MostrarOrcamentoFalse_HidesMoneyFields()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        var dto = await service.GetBySlugAsync(rep.PublicSlug!);

        dto.OrcamentoCents.Should().BeNull();
        dto.PrecoFinalCents.Should().BeNull();
        dto.TemPrecoFinal.Should().BeFalse();
    }

    [Fact]
    public async Task AprovarOrcamentoAsync_WhenDisabled_ThrowsForbidden()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { PermitirAprovarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        var act = () => service.AprovarOrcamentoAsync(rep.PublicSlug!, true);

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ForbiddenException>()
            .Where(e => e.Code == "orcamento_aprovacao_desactivada");
    }

    private static async Task<Reparacao> SeedRepairAsync(AppDbContext db, Guid tenantId)
    {
        var tenant = new Tenant { Id = tenantId, Name = "LopesTech" };
        var cliente = new Cliente { TenantId = tenantId, Nome = "Bruno Lopes", Telefone = "910000000" };
        var rep = new Reparacao
        {
            TenantId = tenantId,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Numero = 1,
            Equipamento = "iPhone 13",
            Avaria = "Ecra partido",
            Diagnostico = "Trocar ecra",
            Estado = RepairStatus.Orcamento,
            EstadoSince = DateTime.UtcNow,
            OrcamentoCents = 12000,
            PrecoFinalCents = 12000,
            PublicSlug = $"slug{Guid.NewGuid():N}"[..12],
        };
        rep.Timeline.Add(new ReparacaoEstadoLog
        {
            TenantId = tenantId,
            Reparacao = rep,
            ReparacaoId = rep.Id,
            EstadoTo = RepairStatus.Orcamento,
            MudouEm = DateTime.UtcNow,
        });
        db.Tenants.Add(tenant);
        db.Clientes.Add(cliente);
        db.Reparacoes.Add(rep);
        await db.SaveChangesAsync();
        return rep;
    }

    private static PublicPortalService NewService(AppDbContext db, Guid tenantId, TenantPreferencesRoot prefs)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var reparacoes = new ReparacaoRepository(db);
        return new PublicPortalService(
            reparacoes,
            new TenantRepository(db),
            new DiagnosticoRepository(db),
            new GarantiaRepository(db),
            new AvaliacaoRepository(db),
            new ReparacaoFotoRepository(db),
            new EquipmentFieldService(new EquipmentFieldRepository(db), reparacoes, tenantContext),
            new VendaRepository(db),
            new FakeTenantPreferencesService(prefs));
    }

    private static AppDbContext NewDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"portal-prefs-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }

    private sealed class FakeTenantPreferencesService(TenantPreferencesRoot prefs) : ITenantPreferencesService
    {
        public Task<TenantPreferencesRoot> GetAsync(CancellationToken ct = default) => Task.FromResult(prefs);
        public Task<TenantPreferencesRoot> GetForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(prefs);
        public Task<TenantPreferencesRoot> UpdateAsync(TenantPreferencesRoot preferences, CancellationToken ct = default) => Task.FromResult(preferences);
        public Task<TenantPreferencesRoot> ResetGroupAsync(string group, CancellationToken ct = default) => Task.FromResult(prefs);
    }
}
