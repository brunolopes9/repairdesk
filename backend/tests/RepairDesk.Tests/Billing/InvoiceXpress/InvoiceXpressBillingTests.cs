using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;

namespace RepairDesk.Tests.Billing.InvoiceXpress;

public class InvoiceXpressBillingTests
{
    [Fact]
    public async Task InvoiceXpressClient_InsertInvoice_UsesAccountNameApiKeyAndReturnsPdf()
    {
        var handler = new QueueHttpHandler(
            Json(HttpStatusCode.Created, """{"invoice":{"id":321,"sequence_number":"FT 2026/321"}}"""),
            Json(HttpStatusCode.OK, """{"output":{"pdfUrl":"https://ix.test/ft-321.pdf"}}"""));

        var client = NewClient(handler);
        var result = await client.InsertInvoiceAsync(Settings(), new InvoiceXpressInvoiceDraft(
            new InvoiceXpressClientDraft("Bruno", "bruno@example.test", "123456789", null),
            "Reparacao #1",
            "Reparacao iPhone",
            "Ecra",
            3990,
            23m,
            "MBWay"));

        result.ExternalId.Should().Be("simplified_invoices:321");
        result.Number.Should().Be("FT 2026/321");
        result.PdfUrl.Should().Be("https://ix.test/ft-321.pdf");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.ToString()
            .Should().Be("https://repairdesk-test.app.invoicexpress.test/simplified_invoices.json?api_key=secret-token");
        var body = handler.Bodies[0];
        body.Should().Contain("\"sequence_id\":\"20\"");
        body.Should().Contain("\"name\":\"Reparacao iPhone\"");
    }

    [Fact]
    public async Task InvoiceXpressBillingProvider_EmitReparacao_SavesProviderAndExternalId()
    {
        var tenantId = Guid.NewGuid();
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 7,
            ClienteId = Guid.NewGuid(),
            Cliente = new Cliente { TenantId = tenantId, Nome = "Cliente Teste", Nif = "123456789" },
            Equipamento = "iPhone 12",
            Avaria = "Ecra partido",
            EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 8900,
        };
        var ix = new FakeInvoiceXpressClient();
        var provider = new InvoiceXpressBillingProvider(
            new FakeReparacaoRepository(reparacao),
            new FakeTrabalhoRepository(),
            new FakeVendaRepository(),
            new FakeSettingsRepository(Settings(tenantId)),
            new FakeTenantRepository(new Tenant { Id = tenantId, Name = "LopesTech", RegimeFiscal = RegimeFiscal.RegimeNormalIva }),
            new FakeTenantContext(tenantId),
            ix);

        var invoice = await provider.EmitReparacaoInvoiceAsync(reparacao.Id, null, "MBWay");

        invoice.Number.Should().Be("FT 2026/321");
        reparacao.InvoiceProvider.Should().Be(BillingProvider.InvoiceXpress);
        reparacao.InvoiceExternalId.Should().Be("simplified_invoices:321");
        reparacao.InvoicePdfUrl.Should().Be("https://ix.test/ft-321.pdf");
        ix.InsertCalls.Should().Be(1);
        ix.LastDraft!.Client.FiscalId.Should().Be("123456789");
    }

    private static InvoiceXpressClient NewClient(QueueHttpHandler handler)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:InvoiceXpress:BaseUrl"] = "https://{account_name}.app.invoicexpress.test",
                ["Billing:InvoiceXpress:SandboxBaseUrl"] = "https://{account_name}.app.invoicexpress.test",
            })
            .Build();

        return new InvoiceXpressClient(
            new HttpClient(handler),
            new PrefixSecretProtector(),
            cfg,
            NullLogger<InvoiceXpressClient>.Instance);
    }

    private static TenantBillingSettings Settings(Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? Guid.NewGuid(),
        Provider = BillingProvider.InvoiceXpress,
        ApiKeyCipherText = "enc:secret-token",
        ClientId = "repairdesk-test",
        DefaultDocumentType = BillingDocumentType.FaturaSimplificada,
        DefaultSerieId = 20,
        SandboxMode = true,
        ExemptionReason = "M01",
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class QueueHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        public QueueHttpHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private sealed class PrefixSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc:{plaintext}";
        public string Unprotect(string cipherText) => cipherText.StartsWith("enc:", StringComparison.Ordinal) ? cipherText[4..] : cipherText;
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public bool HasTenant => true;
    }

    private sealed class FakeSettingsRepository(TenantBillingSettings settings) : ITenantBillingSettingsRepository
    {
        public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantBillingSettings?>(settings);

        public Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
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
        public Task<IReadOnlyList<Reparacao>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
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
        public Task<IReadOnlyList<Trabalho>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Trabalho>)Array.Empty<Trabalho>());
        public void Remove(Trabalho trabalho) { }
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeVendaRepository : IVendaRepository
    {
        public Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
        public Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
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

    private sealed class FakeInvoiceXpressClient : IInvoiceXpressClient
    {
        public int InsertCalls { get; private set; }
        public InvoiceXpressInvoiceDraft? LastDraft { get; private set; }

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
        {
            InsertCalls++;
            LastDraft = draft;
            return Task.FromResult(new InvoiceXpressInvoiceResult("simplified_invoices:321", "FT 2026/321", "https://ix.test/ft-321.pdf", DateTime.UtcNow));
        }
        public Task<InvoiceXpressInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, InvoiceXpressCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new InvoiceXpressInvoiceResult("credit_notes:1", "NC 2026/1", null, DateTime.UtcNow));
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, string externalId, string reason, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string externalId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
    }
}
