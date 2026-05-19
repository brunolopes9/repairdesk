using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;

namespace RepairDesk.Tests.Billing;

public class MoloniBillingTests
{
    [Fact]
    public async Task MoloniClient_InsertInvoice_Success_ReturnsDocumentNumberAndPdf()
    {
        var handler = new QueueHttpHandler(
            Json(HttpStatusCode.OK, """{"document_id":123}"""),
            Json(HttpStatusCode.OK, """{"document_type":{"saft_code":"FA"},"year":2026,"number":123}"""),
            Json(HttpStatusCode.OK, """{"url":"https://moloni.test/fa-123.pdf"}"""));

        var client = NewMoloniClient(handler);
        var result = await client.InsertInvoiceAsync(Settings(), new MoloniInvoiceDraft(
            77, "Reparacao #1", "Reparacao iPhone", "Ecra", 3990, 23m, "MBWay"));

        result.ExternalId.Should().Be("123");
        result.Number.Should().Be("FA 2026/123");
        result.PdfUrl.Should().Be("https://moloni.test/fa-123.pdf");
        handler.Requests.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task MoloniClient_InsertInvoice_HttpError_Throws(HttpStatusCode status)
    {
        var handler = new QueueHttpHandler(Json(status, """{"error":"invalid","error_description":"falhou"}"""));
        var client = NewMoloniClient(handler);

        var act = () => client.InsertInvoiceAsync(Settings(), new MoloniInvoiceDraft(
            77, "R#1", "Servico", null, 1000, 23m, null));

        await act.Should().ThrowAsync<BillingProviderException>()
            .WithMessage($"*{(int)status}*falhou*");
    }

    [Fact]
    public async Task MoloniClient_PostAsync_OnAuthFailure_RefreshesTokenAndRetries()
    {
        var settings = Settings();
        settings.ClientId = "repairdesk-test";
        settings.ClientSecretCipherText = "enc:client-secret";
        settings.RefreshTokenCipherText = "enc:old-refresh";

        var repo = new FakeSettingsRepository(settings);

        var handler = new QueueHttpHandler(
            // 1st call: returns invalid_token -> triggers refresh
            Json(HttpStatusCode.OK, """{"error":"invalid_token","error_description":"Token expired"}"""),
            // 2nd call: refresh endpoint returns fresh tokens
            Json(HttpStatusCode.OK, """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"token_type":"bearer"}"""),
            // 3rd call: retry the original POST -> success
            Json(HttpStatusCode.OK, """{"document_set_id":99,"name":"M","active_by_default":1}"""));

        var client = NewMoloniClient(handler, repo);
        var series = await client.GetSeriesAsync(settings);

        series.Should().NotBeNull();
        handler.Requests.Should().HaveCount(3);

        settings.ApiKeyCipherText.Should().NotBeNullOrWhiteSpace();
        settings.ApiKeyCipherText.Should().NotBe("enc:token");
        settings.RefreshTokenCipherText.Should().NotBe("enc:old-refresh");
    }

    [Fact]
    public async Task MoloniClient_InsertInvoice_MoloniValidationError_Throws()
    {
        var handler = new QueueHttpHandler(Json(HttpStatusCode.OK, """{"valid":0,"errors":[{"description":"Serie invalida"}]}"""));
        var client = NewMoloniClient(handler);

        var act = () => client.InsertInvoiceAsync(Settings(), new MoloniInvoiceDraft(
            77, "R#1", "Servico", null, 1000, 23m, null));

        await act.Should().ThrowAsync<BillingProviderException>()
            .WithMessage("*Serie invalida*");
    }

    [Fact]
    public async Task MoloniBillingProvider_ExistingInvoice_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 10,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Cliente" },
            Equipamento = "iPhone",
            Avaria = "Ecra",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 5000,
            InvoiceProvider = BillingProvider.Moloni,
            InvoiceExternalId = "123",
            InvoiceNumber = "FA 2026/123",
            InvoicePdfUrl = "https://moloni.test/fa-123.pdf",
            InvoiceEmittedAt = new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc),
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(reparacao),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni);

        var first = await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null);
        var second = await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null);

        first.Should().BeEquivalentTo(second);
        moloni.InsertCalls.Should().Be(0);
    }

    [Fact]
    public async Task TenantBillingSettingsService_EncryptsApiKeyAtRest()
    {
        var tenantId = Guid.NewGuid();
        var repo = new FakeSettingsRepository(null);
        var protector = NewDataProtectionSecretProtector();
        var service = new TenantBillingSettingsService(
            repo,
            new FakeTenantContext(tenantId),
            protector,
            new FakeMoloniClient(),
            NewMemoryCache(),
            NewConfig(),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            NullLogger<TenantBillingSettingsService>.Instance);

        await service.UpdateMineAsync(new UpdateTenantBillingSettingsRequest(
            BillingProvider.Moloni,
            ApiKey: "moloni-secret-token",
            ClientId: null,
            ClientSecret: null,
            RefreshToken: null,
            CompanyId: 10,
            DefaultDocumentType: BillingDocumentType.FaturaSimplificada,
            DefaultSerieId: 20,
            SandboxMode: true,
            DefaultProductId: 30,
            DefaultTaxId: 40,
            DefaultPaymentMethodId: 50,
            DefaultMaturityDateId: 60,
            FallbackCustomerId: 70,
            ExemptionReason: null));

        repo.Settings!.ApiKeyCipherText.Should().NotBeNullOrWhiteSpace();
        repo.Settings.ApiKeyCipherText.Should().NotBe("moloni-secret-token");
        protector.Unprotect(repo.Settings.ApiKeyCipherText!).Should().Be("moloni-secret-token");

        var dto = await service.GetMineAsync();
        dto.ApiKeyMasked.Should().Be("****");
        dto.HasApiKey.Should().BeTrue();
    }

    private static MoloniClient NewMoloniClient(QueueHttpHandler handler, ITenantBillingSettingsRepository? repo = null)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:Moloni:BaseUrl"] = "https://api.moloni.test/v1",
                ["Billing:Moloni:SandboxBaseUrl"] = "https://api-sandbox.moloni.test/v1",
            })
            .Build();
        return new MoloniClient(
            new HttpClient(handler),
            new PrefixSecretProtector(),
            cfg,
            repo ?? new FakeSettingsRepository(null),
            NullLogger<MoloniClient>.Instance);
    }

    private static TenantBillingSettings Settings(Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? Guid.NewGuid(),
        Provider = BillingProvider.Moloni,
        ApiKeyCipherText = "enc:token",
        CompanyId = 10,
        DefaultDocumentType = BillingDocumentType.FaturaSimplificada,
        DefaultSerieId = 20,
        DefaultProductId = 30,
        DefaultTaxId = 40,
        DefaultPaymentMethodId = 50,
        DefaultMaturityDateId = 60,
        FallbackCustomerId = 70,
    };

    private static DataProtectionSecretProtector NewDataProtectionSecretProtector()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var provider = DataProtectionProvider.Create(dir);
        return new DataProtectionSecretProtector(provider);
    }

    private static MemoryDistributedCache NewMemoryCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static IConfiguration NewConfig()
        => new ConfigurationBuilder().Build();

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class QueueHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();

        public QueueHttpHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class PrefixSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc:{plaintext}";
        public string Unprotect(string cipherText) => cipherText.StartsWith("enc:", StringComparison.Ordinal) ? cipherText[4..] : cipherText;
    }

    private sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public bool HasTenant => tenantId is not null;
    }

    private sealed class FakeSettingsRepository(TenantBillingSettings? settings) : ITenantBillingSettingsRepository
    {
        public TenantBillingSettings? Settings { get; private set; } = settings;

        public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Settings);

        public Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReparacaoRepository(Reparacao reparacao) : IReparacaoRepository
    {
        public Task<Reparacao?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Reparacao?>(reparacao);
        public Task<Reparacao?> FindByIdWithTimelineAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Reparacao?>(reparacao);
        public Task<Reparacao?> FindByPublicSlugWithTimelineAsync(string slug, CancellationToken ct = default) => Task.FromResult<Reparacao?>(null);
        public Task CreateWithNextNumeroAsync(Reparacao reparacao, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Reparacao> Items, int Total)> SearchAsync(string? query, RepairStatus? estado, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>(), 0));
        public Task<IReadOnlyList<Reparacao>> SearchByImeiAsync(string imeiNormalizado, Guid? excludeId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>());
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Reparacao>> ExportAllAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>());
        public void Remove(Reparacao reparacao) { }
        public void AddEstadoLog(ReparacaoEstadoLog log) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTrabalhoRepository : ITrabalhoRepository
    {
        public Task<Trabalho?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Trabalho?>(null);
        public Task CreateWithNextNumeroAsync(Trabalho trabalho, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Trabalho> Items, int Total)> SearchAsync(string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Trabalho>)Array.Empty<Trabalho>(), 0));
        public void Remove(Trabalho trabalho) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeVendaRepository : IVendaRepository
    {
        public Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
        public Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
        public Task CreateWithNextNumeroAsync(Venda venda, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Venda> Items, int Total)> SearchAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Venda>)Array.Empty<Venda>(), 0));
        public Task<int> SumPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<TopVendaItemRow>> TopItemsByRevenueAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TopVendaItemRow>)Array.Empty<TopVendaItemRow>());
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Tenant?>(tenant);
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMoloniClient : IMoloniClient
    {
        public int InsertCalls { get; private set; }
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
            => Task.FromResult<int?>(settings.FallbackCustomerId);
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
        {
            InsertCalls++;
            return Task.FromResult(new MoloniInvoiceResult("123", "FA 2026/123", "https://moloni.test/fa-123.pdf", DateTime.UtcNow));
        }
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
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
}
