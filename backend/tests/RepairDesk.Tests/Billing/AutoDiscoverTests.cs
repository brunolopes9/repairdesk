using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;

namespace RepairDesk.Tests.Billing;

public class AutoDiscoverTests
{
    [Fact]
    public async Task AutoDiscoverAsync_SelectsExistingServiceProductAndOperationalIds()
    {
        var tenant = Tenant(RegimeFiscal.RegimeNormalIva);
        var settings = Settings(tenant.Id);
        var moloni = CompleteMoloni();
        moloni.Products = new[] { new MoloniProductDto(101, "Serviço", true) };

        var result = await Service(settings, tenant, moloni).AutoDiscoverAsync();

        settings.DefaultProductId.Should().Be(101);
        settings.DefaultTaxId.Should().Be(201);
        settings.DefaultPaymentMethodId.Should().Be(301);
        settings.DefaultMaturityDateId.Should().Be(401);
        settings.FallbackCustomerId.Should().Be(501);
        moloni.InsertProductCalls.Should().Be(0);
        moloni.InsertCustomerCalls.Should().Be(0);
        result.Steps.Should().ContainEquivalentOf(new { Key = "product", Success = true, Created = false, Id = 101 });
    }

    [Fact]
    public async Task AutoDiscoverAsync_CreatesRepairServiceProductWhenMissing()
    {
        var tenant = Tenant(RegimeFiscal.RegimeNormalIva);
        var settings = Settings(tenant.Id);
        var moloni = CompleteMoloni();
        moloni.Products = Array.Empty<MoloniProductDto>();
        moloni.InsertedProduct = new MoloniProductDto(901, "Serviço de reparação", true);

        var result = await Service(settings, tenant, moloni).AutoDiscoverAsync();

        settings.DefaultProductId.Should().Be(901);
        moloni.InsertProductCalls.Should().Be(1);
        result.Steps.Should().ContainEquivalentOf(new { Key = "product", Success = true, Created = true, Id = 901 });
    }

    [Fact]
    public async Task AutoDiscoverAsync_ForVatExemptTenantSelectsZeroTaxAndM01()
    {
        var tenant = Tenant(RegimeFiscal.IsentoArt53);
        var settings = Settings(tenant.Id);
        var moloni = CompleteMoloni();
        moloni.Taxes = new[]
        {
            new MoloniTaxDto(201, "IVA 23%", 23m, true, null),
            new MoloniTaxDto(202, "Isento M01", 0m, true, "M01"),
        };

        await Service(settings, tenant, moloni).AutoDiscoverAsync();

        settings.DefaultTaxId.Should().Be(202);
        settings.ExemptionReason.Should().Be("M01");
    }

    [Fact]
    public async Task AutoDiscoverAsync_DoesNotBlockOtherStepsWhenOneStepFails()
    {
        var tenant = Tenant(RegimeFiscal.RegimeNormalIva);
        var settings = Settings(tenant.Id);
        var moloni = CompleteMoloni();
        moloni.Taxes = Array.Empty<MoloniTaxDto>();

        var result = await Service(settings, tenant, moloni).AutoDiscoverAsync();

        settings.DefaultProductId.Should().Be(101);
        settings.DefaultPaymentMethodId.Should().Be(301);
        settings.DefaultMaturityDateId.Should().Be(401);
        settings.FallbackCustomerId.Should().Be(501);
        result.Steps.Should().ContainEquivalentOf(new { Key = "tax", Success = false });
        result.Steps.Count(step => step.Success).Should().Be(4);
    }

    private static TenantBillingSettingsService Service(TenantBillingSettings settings, Tenant tenant, FakeMoloniClient moloni)
        => new(
            new FakeSettingsRepository(settings),
            new FakeTenantContext(tenant.Id),
            new FakeSecretProtector(),
            moloni,
            new FakeInvoiceXpressClient(),
            NewMemoryCache(),
            new ConfigurationBuilder().Build(),
            new FakeTenantRepository(tenant),
            NullLogger<TenantBillingSettingsService>.Instance);

    private static Tenant Tenant(RegimeFiscal regimeFiscal) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Loja Teste",
        RegimeFiscal = regimeFiscal,
    };

    private static TenantBillingSettings Settings(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Provider = BillingProvider.Moloni,
        ApiKeyCipherText = "token",
        RefreshTokenCipherText = "refresh",
        CompanyId = 10,
        DefaultDocumentType = BillingDocumentType.FaturaSimplificada,
        SandboxMode = true,
    };

    private static FakeMoloniClient CompleteMoloni() => new()
    {
        Products = new[] { new MoloniProductDto(101, "Serviço de reparação", true) },
        Taxes = new[] { new MoloniTaxDto(201, "IVA 23%", 23m, true, null) },
        PaymentMethods = new[] { new MoloniPaymentMethodDto(301, "Numerário", true) },
        MaturityDates = new[] { new MoloniMaturityDateDto(401, "Pronto pagamento", true) },
        Customers = new[] { new MoloniCustomerDto(501, "Consumidor Final", "999999990", true) },
    };

    private static MemoryDistributedCache NewMemoryCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private sealed class FakeSettingsRepository(TenantBillingSettings settings) : ITenantBillingSettingsRepository
    {
        public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantBillingSettings?>(settings);

        public Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public bool HasTenant => true;
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Tenant?>(tenant);

        public Task SaveAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class FakeMoloniClient : IMoloniClient
    {
        public IReadOnlyList<MoloniProductDto> Products { get; set; } = Array.Empty<MoloniProductDto>();
        public IReadOnlyList<MoloniTaxDto> Taxes { get; set; } = Array.Empty<MoloniTaxDto>();
        public IReadOnlyList<MoloniPaymentMethodDto> PaymentMethods { get; set; } = Array.Empty<MoloniPaymentMethodDto>();
        public IReadOnlyList<MoloniMaturityDateDto> MaturityDates { get; set; } = Array.Empty<MoloniMaturityDateDto>();
        public IReadOnlyList<MoloniCustomerDto> Customers { get; set; } = Array.Empty<MoloniCustomerDto>();
        public MoloniProductDto InsertedProduct { get; set; } = new(901, "Serviço de reparação", true);
        public MoloniCustomerDto InsertedCustomer { get; set; } = new(902, "Consumidor Final", "999999990", true);
        public int InsertProductCalls { get; private set; }
        public int InsertCustomerCalls { get; private set; }

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
        public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCompanyDto>)Array.Empty<MoloniCompanyDto>());
        public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult(Products);
        public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult(Taxes);
        public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult(PaymentMethods);
        public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult(MaturityDates);
        public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult(Customers);
        public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
        {
            InsertProductCalls++;
            return Task.FromResult(InsertedProduct with { Name = name });
        }
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default)
        {
            InsertCustomerCalls++;
            return Task.FromResult(InsertedCustomer with { Name = name, Vat = vat });
        }
    }

    private sealed class FakeInvoiceXpressClient : IInvoiceXpressClient
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
}
