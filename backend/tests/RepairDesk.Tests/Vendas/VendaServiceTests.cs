using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;
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
    public async Task EmitirFaturaAsync_ExistingInvoice_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var venda = new Venda
        {
            TenantId = tenantId,
            Numero = 1,
            Status = VendaStatus.Paga,
            TotalCents = 1000,
            IvaCents = 187,
            InvoiceExternalId = "123",
            InvoiceNumber = "FA 2026/123",
            InvoicePdfUrl = "https://moloni.test/fa.pdf",
            InvoiceEmittedAt = DateTime.UtcNow,
        };
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        var billing = new FakeBillingProvider();
        var service = NewService(db, tenantId, billing);

        var invoice = await service.EmitirFaturaAsync(venda.Id);

        invoice.Number.Should().Be("FA 2026/123");
        billing.EmitVendaCalls.Should().Be(0);
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

    [Fact]
    public async Task MarcarPagaAsync_EmitirFaturaAutomatico_EmitsInvoiceWithoutRequestFlag()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Capa", QtdStock = 1, CustoUnitarioCents = 300 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Sales = prefs.Sales with { EmitirFatura = EmitirFaturaMode.Automatico } };
        var billing = new FakeBillingProvider();
        var service = NewService(db, tenantId, billing, prefs, BillingProvider.Moloni);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 1, 1000, 0, 23)
        ], null));

        var result = await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay, EmitirFatura: false));

        result.Invoice.Should().NotBeNull();
        billing.EmitVendaCalls.Should().Be(1);
    }

    [Fact]
    public async Task MarcarPagaAsync_EmitirFaturaNunca_IgnoresRequestFlag()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Capa", QtdStock = 1, CustoUnitarioCents = 300 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Sales = prefs.Sales with { EmitirFatura = EmitirFaturaMode.Nunca } };
        var billing = new FakeBillingProvider();
        var service = NewService(db, tenantId, billing, prefs, BillingProvider.Moloni);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 1, 1000, 0, 23)
        ], null));

        var result = await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay, EmitirFatura: true));

        result.Invoice.Should().BeNull();
        billing.EmitVendaCalls.Should().Be(0);
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
        IBillingProvider? billing = null,
        TenantPreferencesRoot? prefs = null,
        BillingProvider configuredProvider = BillingProvider.None)
        => new(
            new VendaRepository(db),
            new PartRepository(db),
            new ClienteRepository(db),
            new TestTenantContext(tenantId),
            new FakeBillingSettingsRepository(configuredProvider),
            billing ?? new FakeBillingProvider(),
            new FakeMoloniNoOp(),
            new FakeInvoiceXpressNoOp(),
            new GarantiaRepository(db),
            new TenantRepository(db),
            new ReparacaoRepository(db),
            new NoOpWebhookPublisher(),
            new FakeTenantPreferencesService(prefs),
            new NoOpPaymentService());

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }

    private sealed class NoOpWebhookPublisher : RepairDesk.Services.Webhooks.IWebhookPublisher
    {
        public Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class FakeBillingSettingsRepository(BillingProvider provider) : ITenantBillingSettingsRepository
    {
        public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(provider == BillingProvider.None
                ? null
                : new TenantBillingSettings { TenantId = tenantId, Provider = provider });
        public Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBillingProvider : IBillingProvider
    {
        public int EmitVendaCalls { get; private set; }
        public Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default)
        {
            EmitVendaCalls++;
            return Task.FromResult(new InvoiceDto("FA 2026/1", null, DateTime.UtcNow));
        }
        public Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
    }

    /// <summary>No-op IMoloniClient para testes — só serve para satisfazer DI.</summary>
    private sealed class FakeMoloniNoOp : IMoloniClient
    {
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
            => Task.FromResult<int?>(null);
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("1", "FA 2026/1", null, DateTime.UtcNow));
        public Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniEstimateResult("E1", "OR 2026/1", null, DateTime.UtcNow));
        public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(TenantBillingSettings settings, int estimateId, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("1", "FA 2026/1", null, DateTime.UtcNow));
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("NC1", "NC 2026/1", null, DateTime.UtcNow));
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCompanyDto>)Array.Empty<MoloniCompanyDto>());
        public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniProductDto>)Array.Empty<MoloniProductDto>());
        public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniTaxDto>)Array.Empty<MoloniTaxDto>());
        public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniPaymentMethodDto>)Array.Empty<MoloniPaymentMethodDto>());
        public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniMaturityDateDto>)Array.Empty<MoloniMaturityDateDto>());
        public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCustomerDto>)Array.Empty<MoloniCustomerDto>());
        public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
            => Task.FromResult(new MoloniProductDto(1, name, true));
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default)
            => Task.FromResult(new MoloniCustomerDto(1, name, vat, true));
    }

    private sealed class FakeInvoiceXpressNoOp : IInvoiceXpressClient
    {
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<IReadOnlyList<InvoiceXpressClientDto>> GetClientsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<InvoiceXpressClientDto>)Array.Empty<InvoiceXpressClientDto>());
        public Task<IReadOnlyList<InvoiceXpressItemDto>> GetItemsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<InvoiceXpressItemDto>)Array.Empty<InvoiceXpressItemDto>());
        public Task<IReadOnlyList<InvoiceXpressDocumentDto>> ListInvoicesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<InvoiceXpressDocumentDto>)Array.Empty<InvoiceXpressDocumentDto>());
        public Task<InvoiceXpressInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, InvoiceXpressInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new InvoiceXpressInvoiceResult("1", "FT 2026/1", null, DateTime.UtcNow));
        public Task<InvoiceXpressInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, InvoiceXpressCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new InvoiceXpressInvoiceResult("2", "NC 2026/1", null, DateTime.UtcNow));
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, string externalId, string reason, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string externalId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class NoOpPaymentService : IPaymentService
    {
        public Task<Payment> InitiateAsync(PaymentInitiationRequest request, PaymentProvider provider, CancellationToken ct = default)
            => Task.FromResult(new Payment { Id = Guid.NewGuid(), TenantId = request.TenantId, VendaId = request.VendaId });
        public Task<Payment?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Payment?>(null);
        public Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Payment>>(Array.Empty<Payment>());
        public Task<Payment> ApplyStatusUpdateAsync(string providerRef, PaymentStatusSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new Payment { Id = Guid.NewGuid() });
    }
}
