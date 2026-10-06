using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Payments;
using RepairDesk.Services.TenantPreferences;
using RepairDesk.Services.Vendas;
using DomainValidationException = RepairDesk.Core.Exceptions.ValidationException;

namespace RepairDesk.Tests.Vendas;

public class VendaServiceTests
{
    [Fact]
    public async Task CreateAsync_OverStock_ThrowsValidation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Capa", QtdStock = 1, CustoUnitarioCents = 500 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var act = () => service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 2, 1000, 0, 23)
        ], null));

        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(e => e.Code == "stock_insuficiente");
    }

    [Fact]
    public async Task MarcarPagaAsync_DecrementsStockAndCreatesMovement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Película", QtdStock = 3, CustoUnitarioCents = 400 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 2, 1000, 0, 23)
        ], null));

        await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay));

        var updated = await db.Parts.SingleAsync(p => p.Id == part.Id);
        updated.QtdStock.Should().Be(1);
        db.PartMovimentos.Should().ContainSingle(m =>
            m.PartId == part.Id &&
            m.VendaId == venda.Id &&
            m.Quantidade == -2 &&
            m.Motivo == PartMovimentoMotivo.VendaCliente);
    }

    [Fact]
    public async Task MarcarPagaAsync_AutoEmiteGarantiaComDefaultDoTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "LopesTech",
            GarantiaVendaDiasDefault = 1095,
            GarantiaVendaCoberturaDefault = "Cobertura customizada",
        });
        var part = new Part { TenantId = tenantId, Nome = "Telemovel", QtdStock = 1, CustoUnitarioCents = 12000 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 1, 30000, 0, 23)
        ], null));

        await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay));

        var garantia = await db.Garantias.SingleAsync(g => g.VendaId == venda.Id);
        garantia.SourceType.Should().Be(GarantiaSourceType.Venda);
        garantia.DiasGarantia.Should().Be(1095);
        garantia.ReparacaoId.Should().BeNull();
        garantia.Cobertura.Should().Be("Cobertura customizada");
        (garantia.DataFim - garantia.DataInicio).Days.Should().Be(1095);
    }

    [Fact]
    public async Task MarcarPagaAsync_Idempotente_NaoDuplicaGarantia()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "LopesTech" });
        var part = new Part { TenantId = tenantId, Nome = "Telemovel", QtdStock = 1, CustoUnitarioCents = 12000 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 1, 30000, 0, 23)
        ], null));

        await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay));
        await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay));

        (await db.Garantias.CountAsync(g => g.VendaId == venda.Id)).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_SmartphoneSemImei_LancaValidacao()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "LopesTech" });
        var telemovel = new Part
        {
            TenantId = tenantId,
            Nome = "iPhone 13 Grade A",
            Categoria = PartCategoria.Smartphone,
            QtdStock = 1,
            CustoUnitarioCents = 30000,
        };
        db.Parts.Add(telemovel);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var act = () => service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(telemovel.Id, null, 1, 45000, 0, 23, Imei: null)
        ], null));

        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(e => e.Code == "imei_obrigatorio");
    }

    [Fact]
    public async Task CreateAsync_SmartphoneImeiInvalido_LancaValidacao()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "LopesTech" });
        var telemovel = new Part
        {
            TenantId = tenantId,
            Nome = "iPhone 13",
            Categoria = PartCategoria.Smartphone,
            QtdStock = 1,
            CustoUnitarioCents = 30000,
        };
        db.Parts.Add(telemovel);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var act = () => service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(telemovel.Id, null, 1, 45000, 0, 23, Imei: "123456789012345")
        ], null));

        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(e => e.Code == "imei_invalido");
    }

    [Fact]
    public async Task CreateAsync_SmartphoneImeiValido_PersistsNormalizado()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "LopesTech" });
        var telemovel = new Part
        {
            TenantId = tenantId,
            Nome = "iPhone 13",
            Categoria = PartCategoria.Smartphone,
            QtdStock = 1,
            CustoUnitarioCents = 30000,
        };
        db.Parts.Add(telemovel);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        // IMEI com espaços — deve ser normalizado para apenas dígitos
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(telemovel.Id, null, 1, 45000, 0, 23, Imei: "490 154 203 237 518")
        ], null));

        var item = await db.VendaItems.SingleAsync(i => i.VendaId == venda.Id);
        item.Imei.Should().Be("490154203237518");
    }

    [Fact]
    public async Task CreateAsync_DefaultCondicaoFromPreferences_AppliesToItem()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "iPhone OpenBox", QtdStock = 1, CustoUnitarioCents = 10000 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Sales = prefs.Sales with { DefaultCondicaoArtigo = (int)CondicaoArtigo.OpenBox } };
        var service = NewService(db, tenantId, prefs: prefs);

        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 1, 20000, 0, 23)
        ], null));

        var item = await db.VendaItems.SingleAsync(i => i.VendaId == venda.Id);
        item.Condicao.Should().Be(CondicaoArtigo.OpenBox);
    }

    private static AppDbContext NewDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vendas-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private static VendaService NewService(
        AppDbContext db,
        Guid tenantId,
        TenantPreferencesRoot? prefs = null)
        => new(
            new VendaRepository(db),
            new PartRepository(db),
            new ClienteRepository(db),
            new TestTenantContext(tenantId),
            new GarantiaRepository(db),
            new TenantRepository(db),
            new ReparacaoRepository(db),
            new FakeTenantPreferencesService(prefs),
            new NoOpPaymentService());

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }

    private sealed class FakeTenantPreferencesService : ITenantPreferencesService
    {
        private TenantPreferencesRoot _prefs;
        public FakeTenantPreferencesService(TenantPreferencesRoot? prefs = null)
        {
            _prefs = prefs ?? TenantPreferencesDefaults.Create();
        }

        public Task<TenantPreferencesRoot> GetAsync(CancellationToken ct = default) => Task.FromResult(_prefs);
        public Task<TenantPreferencesRoot> GetForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_prefs);
        public Task<TenantPreferencesRoot> UpdateAsync(TenantPreferencesRoot preferences, CancellationToken ct = default)
        {
            _prefs = preferences;
            return Task.FromResult(_prefs);
        }

        public Task<TenantPreferencesRoot> ResetGroupAsync(string group, CancellationToken ct = default)
        {
            _prefs = TenantPreferencesDefaults.Create();
            return Task.FromResult(_prefs);
        }
    }

    private sealed class NoOpPaymentService : IPaymentService
    {
        public Task<Payment> InitiateAsync(PaymentInitiationRequest request, PaymentProvider provider, CancellationToken ct = default)
            => Task.FromResult(new Payment { Id = Guid.NewGuid(), TenantId = request.TenantId, VendaId = request.VendaId });
        public Task<Payment?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Payment?>(null);
        public Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Payment>>(Array.Empty<Payment>());
        public Task<IReadOnlyList<Payment>> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Payment>>(Array.Empty<Payment>());
        public Task<Payment> ApplyStatusUpdateAsync(string providerRef, PaymentStatusSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new Payment { Id = Guid.NewGuid() });
    }
}
