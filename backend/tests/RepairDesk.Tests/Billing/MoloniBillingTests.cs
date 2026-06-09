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
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;

namespace RepairDesk.Tests.Billing;

public class MoloniBillingTests
{
    [Fact]
    public async Task MoloniClient_InsertInvoice_Success_ReturnsDocumentNumberAndPdf()
    {
        var handler = new QueueHttpHandler(
            // Sprint 505: resolução dinâmica de tax_id chama taxes/getAll antes do insert.
            Json(HttpStatusCode.OK, """{"taxes":[{"tax_id":40,"name":"IVA23","value":23,"active":1}]}"""),
            Json(HttpStatusCode.OK, """{"document_id":123}"""),
            Json(HttpStatusCode.OK, """{"document_type":{"saft_code":"FA"},"year":2026,"number":123}"""),
            Json(HttpStatusCode.OK, """{"url":"https://moloni.test/fa-123.pdf"}"""));

        var client = NewMoloniClient(handler);
        var result = await client.InsertInvoiceAsync(Settings(), new MoloniInvoiceDraft(
            77, "Reparacao #1", "Reparacao iPhone", "Ecra", 3990, 23m, "MBWay"));

        result.ExternalId.Should().Be("123");
        result.Number.Should().Be("FA 2026/123");
        result.PdfUrl.Should().Be("https://moloni.test/fa-123.pdf");
        handler.Requests.Should().HaveCount(4);
    }

    [Fact]
    public async Task MoloniClient_EstimateFlow_InsertsAndConvertsToInvoice()
    {
        var handler = new QueueHttpHandler(
            // Sprint 505: estimates/insert resolve tax_id via taxes/getAll primeiro.
            Json(HttpStatusCode.OK, """{"taxes":[{"tax_id":40,"name":"IVA23","value":23,"active":1}]}"""),
            Json(HttpStatusCode.OK, """{"document_id":456}"""),
            Json(HttpStatusCode.OK, """{"document_type":{"saft_code":"OR"},"year":2026,"number":456}"""),
            Json(HttpStatusCode.OK, """{"url":"https://moloni.test/or-456.pdf"}"""),
            Json(HttpStatusCode.OK, """{"document_id":789}"""),
            Json(HttpStatusCode.OK, """{"document_type":{"saft_code":"FA"},"year":2026,"number":789}"""),
            Json(HttpStatusCode.OK, """{"url":"https://moloni.test/fa-789.pdf"}"""));

        var client = NewMoloniClient(handler);
        var estimate = await client.InsertEstimateAsync(Settings(), new MoloniInvoiceDraft(
            77, "Reparacao #1", "Reparacao iPhone", "Ecra", 3990, 23m, null));

        estimate.ExternalId.Should().Be("456");
        estimate.Number.Should().Be("OR 2026/456");
        estimate.PdfUrl.Should().Be("https://moloni.test/or-456.pdf");

        var invoice = await client.ConvertEstimateToInvoiceAsync(Settings(), 456);

        invoice.ExternalId.Should().Be("789");
        invoice.Number.Should().Be("FA 2026/789");
        invoice.PdfUrl.Should().Be("https://moloni.test/fa-789.pdf");
        handler.Requests.Should().HaveCount(7);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Contain("taxes/getAll");
        handler.Requests[1].RequestUri!.AbsoluteUri.Should().Contain("estimates/insert");
        handler.Requests[4].RequestUri!.AbsoluteUri.Should().Contain("documentsToInvoice");
    }

    [Fact]
    public async Task MoloniClient_GetEstimateStatus_UsesDocumentStatus()
    {
        var handler = new QueueHttpHandler(Json(HttpStatusCode.OK, """{"status":2}"""));
        var client = NewMoloniClient(handler);

        var status = await client.GetEstimateStatusAsync(Settings(), 456);

        status.Should().Be(2);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Contain("documents/getOne");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task MoloniClient_InsertInvoice_HttpError_Throws(HttpStatusCode status)
    {
        var handler = new QueueHttpHandler(
            Json(HttpStatusCode.OK, """{"taxes":[{"tax_id":40,"name":"IVA23","value":23,"active":1}]}"""),
            Json(status, """{"error":"invalid","error_description":"falhou"}"""));
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
        var handler = new QueueHttpHandler(
            Json(HttpStatusCode.OK, """{"taxes":[{"tax_id":40,"name":"IVA23","value":23,"active":1}]}"""),
            Json(HttpStatusCode.OK, """{"valid":0,"errors":[{"description":"Serie invalida"}]}"""));
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
            moloni,
            new FakePartRepository());

        var first = await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null);
        var second = await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null);

        first.Should().BeEquivalentTo(second);
        moloni.InsertCalls.Should().Be(0);
    }

    // Sprint 507: a Fatura Simplificada tem limite legal de €100 (líquido) para não-retalho.
    // O tipo de documento tem de escalar para Fatura quando há NIF OU o líquido passa €100,
    // senão a Moloni rejeita com "net_value must be ... <= 100".
    [Theory]
    // Sprint 519: com método de pagamento configurado (Settings tem DefaultPaymentMethodId=50), os
    // documentos completos saem como Fatura-Recibo (pago) em vez de Fatura pura (a crédito).
    [InlineData("235061921", 5000, BillingDocumentType.FaturaRecibo)]       // NIF → Fatura-Recibo (qualquer valor)
    [InlineData(null, 13500, BillingDocumentType.FaturaRecibo)]             // €135 (líquido 109,76 > €100) → Fatura-Recibo
    [InlineData(null, 5000, BillingDocumentType.FaturaSimplificada)]        // €50 sem NIF → default do tenant
    public async Task EmitReparacaoInvoice_EscolheTipoDocumentoPorNifELimite(string? nif, int precoCents, BillingDocumentType esperado)
    {
        var tenantId = Guid.NewGuid();
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            // Sprint 511: cliente com NIF precisa de morada (guard CIVA art. 36.º); sem NIF não precisa.
            Cliente = new Cliente { TenantId = tenantId, Nome = "Cliente", Nif = nif, Morada = nif is null ? null : "Rua de Teste, 10" },
            Equipamento = "iPhone",
            Avaria = "Ecra",
            Diagnostico = "Substituição de ecrã",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = precoCents,
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(reparacao),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),       // DefaultDocumentType = FaturaSimplificada
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null, discriminarMaoObra: false);

        moloni.LastDraft.Should().NotBeNull();
        moloni.LastDraft!.DocumentTypeOverride.Should().Be(esperado);
    }

    // Sprint 511: uma Fatura com NIF exige a morada do adquirente (CIVA art. 36.º n.º 5). Sem morada,
    // emitir tem de FALHAR com erro claro — em vez de produzir "Consumidor final / 0000-000" no Moloni.
    [Fact]
    public async Task EmitReparacaoInvoice_FaturaComNif_SemMorada_Recusa()
    {
        var tenantId = Guid.NewGuid();
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Maria", Nif = "235061921" }, // NIF mas SEM morada
            Equipamento = "iPhone",
            Avaria = "Ecra",
            Diagnostico = "Substituição de ecrã",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 13500,
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(reparacao),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        var act = async () => await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null, discriminarMaoObra: false);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Code.Should().Be("fatura_nif_sem_morada");
        moloni.InsertCalls.Should().Be(0); // nunca chegou a tocar no Moloni
    }

    // Sprint 519: a Fatura-Recibo exige método de pagamento (array payments). Sem ele configurado,
    // o fallback tem de ser a Fatura pura (a crédito) — nunca uma FR inválida que o Moloni rejeitaria.
    [Fact]
    public async Task EmitReparacaoInvoice_SemMetodoPagamento_CaiEmFaturaPura()
    {
        var tenantId = Guid.NewGuid();
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Maria", Nif = "235061921", Morada = "Rua X, 1" },
            Equipamento = "iPhone",
            Avaria = "Ecra",
            Diagnostico = "Substituição de ecrã",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 13500,
        };

        var settings = Settings(tenantId);
        settings.DefaultPaymentMethodId = null; // sem método de pagamento → FR impossível

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(reparacao),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(),
            new FakeSettingsRepository(settings),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, null, discriminarMaoObra: false);

        moloni.LastDraft!.DocumentTypeOverride.Should().Be(BillingDocumentType.Fatura);
    }

    // Sprint 533: faturação de serviços (ex.: software) via Trabalhos — a escolha explícita do tipo de
    // documento MANDA; só auto-resolve quando não há escolha. Espelha EmitReparacaoInvoice.
    [Theory]
    [InlineData(null, BillingDocumentType.FaturaRecibo)]                              // sem escolha + NIF + método pagamento → FR
    [InlineData(BillingDocumentType.Fatura, BillingDocumentType.Fatura)]             // escolha Fatura manda
    [InlineData(BillingDocumentType.FaturaRecibo, BillingDocumentType.FaturaRecibo)] // escolha Fatura-Recibo manda
    public async Task EmitTrabalhoInvoice_RespeitaDocumentTypeOverride(BillingDocumentType? escolha, BillingDocumentType esperado)
    {
        var tenantId = Guid.NewGuid();
        var trabalho = new Trabalho
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Empresa Lda", Nif = "501234567", Morada = "Rua do Software, 1" },
            Titulo = "Desenvolvimento de software",
            Descricao = "Projeto X",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 150000,
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(new Reparacao { TenantId = tenantId, Equipamento = "n/a", Avaria = "n/a" }),
            new FakeTrabalhoRepository(trabalho),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        await provider.EmitTrabalhoInvoiceAsync(trabalho.Id, null, null, escolha);

        moloni.LastDraft.Should().NotBeNull();
        moloni.LastDraft!.DocumentTypeOverride.Should().Be(esperado);
    }

    // Sprint 533: guarda fiscal — Fatura/Fatura-Recibo de Trabalho com NIF exige morada (CIVA art. 36.º n.º 5).
    [Fact]
    public async Task EmitTrabalhoInvoice_FaturaComNif_SemMorada_Recusa()
    {
        var tenantId = Guid.NewGuid();
        var trabalho = new Trabalho
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Empresa Lda", Nif = "501234567" }, // NIF mas SEM morada
            Titulo = "Desenvolvimento de software",
            Descricao = "Projeto X",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 150000,
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(new Reparacao { TenantId = tenantId, Equipamento = "n/a", Avaria = "n/a" }),
            new FakeTrabalhoRepository(trabalho),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        var act = async () => await provider.EmitTrabalhoInvoiceAsync(trabalho.Id, null, null, BillingDocumentType.Fatura);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Code.Should().Be("fatura_nif_sem_morada");
        moloni.InsertCalls.Should().Be(0); // nunca chegou a tocar no Moloni
    }

    // Sprint 534: Regime da margem — bens em segunda mão (CIVA art. 308.º / motivo M13). As linhas de
    // artigos recondicionados/usados saem a 0% IVA + exemption_reason=M13; as restantes (novo/acessório)
    // mantêm o IVA normal no MESMO documento.
    [Fact]
    public async Task EmitVendaInvoice_BensSegundaMao_SaemEmRegimeMargemM13()
    {
        var tenantId = Guid.NewGuid();
        var venda = new Venda
        {
            TenantId = tenantId,
            Numero = 1,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Cliente" }, // sem NIF → sem guarda de morada
            Status = VendaStatus.Paga,
            TotalCents = 6000,
            Items = new List<VendaItem>
            {
                new() { TenantId = tenantId, Descricao = "iPhone 12 recondicionado", Quantidade = 1, PrecoUnitarioCents = 5000, IvaRate = 23m, Condicao = CondicaoArtigo.Recondicionado },
                new() { TenantId = tenantId, Descricao = "Capa", Quantidade = 1, PrecoUnitarioCents = 1000, IvaRate = 23m, Condicao = CondicaoArtigo.Novo },
            },
        };

        var moloni = new FakeMoloniClient();
        var provider = new MoloniBillingProvider(
            new FakeReparacaoRepository(new Reparacao { TenantId = tenantId, Equipamento = "n/a", Avaria = "n/a" }),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(venda),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "Tenant" }),
            new FakeTenantContext(tenantId),
            moloni,
            new FakePartRepository());

        await provider.EmitVendaInvoiceAsync(venda.Id);

        var linhas = moloni.LastDraft!.Items!;
        var recond = linhas.Single(l => l.Name.Contains("recondicionado"));
        recond.VatPercent.Should().Be(0m);
        recond.ExemptionReason.Should().Be("M13");

        var capa = linhas.Single(l => l.Name == "Capa");
        capa.VatPercent.Should().Be(23m);
        capa.ExemptionReason.Should().BeNull();
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
            new FakeInvoiceXpressClient(),
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
        public Task<(IReadOnlyList<Reparacao> Items, int Total)> SearchAsync(string? query, RepairStatus? estado, Guid? clienteId, int page, int pageSize, CancellationToken ct = default, DeviceCategory? categoria = null)
            => Task.FromResult(((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>(), 0));
        public Task<IReadOnlyList<Reparacao>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>());
        public Task<IReadOnlyList<Reparacao>> SearchByImeiAsync(string imeiNormalizado, Guid? excludeId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>());
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Reparacao>> ExportAllAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Reparacao>)Array.Empty<Reparacao>());
        public void Remove(Reparacao reparacao) { }
        public void AddEstadoLog(ReparacaoEstadoLog log) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTrabalhoRepository(Trabalho? trabalho = null) : ITrabalhoRepository
    {
        public Task<Trabalho?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(trabalho);
        public Task CreateWithNextNumeroAsync(Trabalho trabalho, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Trabalho> Items, int Total)> SearchAsync(string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Trabalho>)Array.Empty<Trabalho>(), 0));
        public Task<IReadOnlyList<Trabalho>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Trabalho>)Array.Empty<Trabalho>());
        public void Remove(Trabalho trabalho) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeVendaRepository(Venda? venda = null) : IVendaRepository
    {
        public Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(venda);
        public Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(venda);
        public Task CreateWithNextNumeroAsync(Venda venda, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<Venda> Items, int Total)> SearchAsync(DateTime? fromUtc, DateTime? toUtc, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Venda>)Array.Empty<Venda>(), 0));
        public Task<int> SumPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<TopVendaItemRow>> TopItemsByRevenueAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TopVendaItemRow>)Array.Empty<TopVendaItemRow>());
        public Task<VendaImeiLookupRow?> FindVendaByImeiAsync(string imei, CancellationToken ct = default) => Task.FromResult<VendaImeiLookupRow?>(null);
        public Task<IReadOnlyList<string>> ListDistinctFornecedoresAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Tenant?>(tenant);
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Sprint 136: stub minimal — testes não exercitam peças, MovimentosAsync devolve vazio.</summary>
    private sealed class FakePartRepository : IPartRepository
    {
        public Task<Part?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Part?>(null);
        public Task<Part?> FindByIdWithMovimentosAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Part?>(null);
        public Task<Part?> FindBySkuAsync(string sku, CancellationToken ct = default) => Task.FromResult<Part?>(null);
        public Task<bool> SkuExistsAsync(string sku, Guid? exceptId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<(IReadOnlyList<Part> Items, int Total)> SearchAsync(string? query, PartCategoria? categoria, string? marca, bool lowStockOnly, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Part>)Array.Empty<Part>(), 0));
        public Task<IReadOnlyList<Part>> LowStockAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Part>)Array.Empty<Part>());
        public Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
        public Task AddAsync(Part part, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(Part part) { }
        public void AddMovimento(PartMovimento movimento) { }
        public Task<IReadOnlyList<PartMovimento>> MovimentosAsync(Guid? partId, Guid? reparacaoId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<PartMovimento>)Array.Empty<PartMovimento>());
        public Task<int> SumCustoByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoesAsync(int days, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<ReabastecerSugestao>)Array.Empty<ReabastecerSugestao>());
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMoloniClient : IMoloniClient
    {
        public int InsertCalls { get; private set; }
        public MoloniInvoiceDraft? LastDraft { get; private set; }
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
            => Task.FromResult<int?>(settings.FallbackCustomerId);
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
        {
            InsertCalls++;
            LastDraft = draft;
            return Task.FromResult(new MoloniInvoiceResult("123", "FA 2026/123", "https://moloni.test/fa-123.pdf", DateTime.UtcNow));
        }
        public Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniEstimateResult("456", "OR 2026/456", "https://moloni.test/or-456.pdf", DateTime.UtcNow));
        public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(TenantBillingSettings settings, int estimateId, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("789", "FA 2026/789", "https://moloni.test/fa-789.pdf", DateTime.UtcNow));
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("NC123", "NC 2026/1", null, DateTime.UtcNow));
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
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default)
            => Task.FromResult(new MoloniCustomerDto(1, name, vat, true));
        public Task<bool> UpdateCustomerAsync(TenantBillingSettings settings, int customerId, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<IReadOnlyList<MoloniDocumentRow>> ListDocumentsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MoloniDocumentRow>>(System.Array.Empty<MoloniDocumentRow>());
        public Task<IReadOnlyList<MoloniDocumentRow>> ListReceiptsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MoloniDocumentRow>>(System.Array.Empty<MoloniDocumentRow>());
        public Task<MoloniReceiptResult> InsertReceiptAsync(TenantBillingSettings settings, int customerId, int documentId, int valueCents, string? notes, CancellationToken ct = default)
            => Task.FromResult(new MoloniReceiptResult(1, "RG/1"));
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
