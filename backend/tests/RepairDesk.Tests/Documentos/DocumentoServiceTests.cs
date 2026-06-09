using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Documentos;

namespace RepairDesk.Tests.Documentos;

public class DocumentoServiceTests
{
    private sealed class FakeRepo(IReadOnlyList<DocumentoVendaRow> rows) : IRelatorioFiscalRepository
    {
        public Task<IReadOnlyList<DocumentoVendaRow>> ListVendaDocumentosDetalheAsync(DateTime f, DateTime t, CancellationToken ct = default)
            => Task.FromResult(rows);

        public Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearInvoiceFieldsAsync(string tipo, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task MarcarReciboEmitidoAsync(string invoiceExternalId, string reciboNumero, DateTime emitidoEm, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> SumPecasCustoComIvaAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SumDespesasComIvaAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IvaDeducaoLinha>> ListComprasStockAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IvaDeducaoLinha>> ListDespesasOpExAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MargemRegimeResult> SumMargemRegimeAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static DocumentoService MakeService(
        IReadOnlyList<DocumentoVendaRow> localRows,
        IReadOnlyList<MoloniDocumentRow>? moloniDocs = null,
        IReadOnlyList<MoloniDocumentRow>? moloniReceipts = null)
    {
        // Sem moloniDocs → tenant null → o fetch ao Moloni é saltado (comportamento local puro).
        Guid? tenantId = moloniDocs is null ? null : Guid.NewGuid();
        var tenant = Mock.Of<ITenantContext>(t => t.TenantId == tenantId);

        TenantBillingSettings? settings = moloniDocs is null
            ? null
            : new TenantBillingSettings { TenantId = tenantId!.Value, Provider = BillingProvider.Moloni, CompanyId = 388093 };
        var settingsRepo = Mock.Of<ITenantBillingSettingsRepository>(r =>
            r.FindByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(settings));

        var docsResult = moloniDocs ?? (IReadOnlyList<MoloniDocumentRow>)System.Array.Empty<MoloniDocumentRow>();
        var receiptsResult = moloniReceipts ?? (IReadOnlyList<MoloniDocumentRow>)System.Array.Empty<MoloniDocumentRow>();
        var moloni = Mock.Of<IMoloniClient>(m =>
            m.ListDocumentsAsync(It.IsAny<TenantBillingSettings>(), It.IsAny<CancellationToken>()) == Task.FromResult(docsResult)
            && m.ListReceiptsAsync(It.IsAny<TenantBillingSettings>(), It.IsAny<CancellationToken>()) == Task.FromResult(receiptsResult));

        return new DocumentoService(
            new FakeRepo(localRows), tenant, settingsRepo, moloni,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<DocumentoService>.Instance);
    }

    private static DocumentoVendaRow Local(string? numero, int total, string origem = "Venda", string? nif = null, string? nome = null, string? externalId = null)
        => new(Guid.NewGuid(), origem, 1, numero, externalId ?? Guid.NewGuid().ToString("N"), "http://pdf", BillingProvider.Moloni, DateTime.UtcNow, Guid.NewGuid(), nome, nif, total);

    private static MoloniDocumentRow MoloniDoc(int docId, string saft, int gross, int net, int taxes, int status = 1, string? nome = null, string? nif = null, int assoc = 0)
        => new(docId, saft, $"{saft} {DateTime.UtcNow.Year}/{docId}", DateTime.UtcNow, nome, nif, gross, net, taxes, status, 0, assoc);

    [Fact]
    public async Task ListVendas_LocalApenas_DerivaTipo_ExtraiIva_SomaTotais()
    {
        var svc = MakeService(new[]
        {
            Local("FT M/2", 13500, nome: "Maria", nif: "235061921"),
            Local("FS 2026/7", 1000),
        });

        var res = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, null));

        res.TotalDocumentos.Should().Be(2);
        res.TotalCents.Should().Be(14500);
        res.Items.Should().Contain(d => d.TipoCodigo == "FT" && d.Tipo == "Fatura");
        res.Items.Should().Contain(d => d.TipoCodigo == "FS" && d.Tipo == "Fatura Simplificada");

        // IVA 23% embutido: 135,00€ -> base 109,76€ + IVA 25,24€ (igual à fatura real da Maria).
        var ft = res.Items.First(d => d.TipoCodigo == "FT");
        ft.BaseCents.Should().Be(10976);
        ft.IvaCents.Should().Be(2524);
    }

    [Fact]
    public async Task ListVendas_FiltraPorTipo_E_Por_Q()
    {
        var svc = MakeService(new[]
        {
            Local("FT M/2", 13500, nome: "Maria", nif: "235061921"),
            Local("FS 2026/7", 1000, nome: "Joao"),
        });

        (await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, "FT"))).TotalDocumentos.Should().Be(1);

        var porNif = await svc.ListVendasAsync(new DocumentosFiltro(null, null, "235061921", null));
        porNif.TotalDocumentos.Should().Be(1);
        porNif.Items[0].ClienteNome.Should().Be("Maria");
    }

    [Fact]
    public async Task ExportCsv_TemCabecalhoEUmaLinha()
    {
        var svc = MakeService(new[] { Local("FT M/2", 13500, nome: "Maria", nif: "235061921") });
        var csv = System.Text.Encoding.UTF8.GetString(await svc.ExportVendasCsvAsync(null, null));
        csv.Should().Contain("total_eur");
        csv.Should().Contain("FT M/2");
        csv.Should().Contain("235061921");
    }

    [Fact]
    public async Task ListVendas_FundeMoloni_SobrepoeValoresReais_E_AdicionaSoMoloni()
    {
        // Local: 1 fatura emitida pelo Mender (externalId 100) com a estimativa 23%.
        // Moloni: a MESMA (doc 100, com valores REAIS distintos da estimativa) + uma NC que só
        // existe no Moloni (doc 200, feita/anulada directamente no painel).
        var svc = MakeService(
            localRows: new[] { Local("FT 2026/2", 13500, nome: "Maria", nif: "235061921", externalId: "100") },
            moloniDocs: new[]
            {
                MoloniDoc(100, "FT", gross: 13500, net: 12501, taxes: 999, status: 1, nome: "Maria", nif: "235061921"),
                MoloniDoc(200, "NC", gross: 13500, net: 12501, taxes: 999, status: 1, nome: "Maria", nif: "235061921"),
            });

        var res = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, null));

        res.TotalDocumentos.Should().Be(2); // FT fundida (não duplicada) + NC só-Moloni

        var ft = res.Items.First(d => d.TipoCodigo == "FT");
        ft.IvaCents.Should().Be(999);     // IVA exacto do Moloni sobrepôs a estimativa (2524)
        ft.BaseCents.Should().Be(12501);
        ft.Origem.Should().Be("Venda");   // mantém o link à origem local

        res.Items.Should().Contain(d => d.TipoCodigo == "NC" && d.Origem == "Moloni");
    }

    [Fact]
    public async Task ListVendas_IncluiRecibos_MasNaoOsSomaAoFaturado()
    {
        // Sprint 529: recibos (RG) vêm do receipts/getAll (família separada no Moloni). Aparecem
        // na lista a par da fatura que liquidam, mas NÃO entram no "faturado" — senão duplicariam
        // a receita (a FT já a contou). Invariante que o Bruno exige: total não inflaciona.
        var svc = MakeService(
            localRows: System.Array.Empty<DocumentoVendaRow>(),
            moloniDocs: new[] { MoloniDoc(100, "FT", gross: 13500, net: 10976, taxes: 2524, status: 1, nome: "Maria") },
            moloniReceipts: new[] { MoloniDoc(300, "RG", gross: 13500, net: 10976, taxes: 2524, status: 1, nome: "Maria") });

        var res = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, null));

        res.TotalDocumentos.Should().Be(2);                                  // FT + RG ambos visíveis
        res.Items.Should().Contain(d => d.TipoCodigo == "RG" && d.Tipo == "Recibo");
        res.TotalCents.Should().Be(13500);                                   // só a FT conta (RG excluído)

        // E o filtro por tipo "RG" devolve só o recibo.
        var soRecibos = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, "RG"));
        soRecibos.TotalDocumentos.Should().Be(1);
        soRecibos.Items[0].TipoCodigo.Should().Be("RG");
    }

    [Fact]
    public async Task ListVendas_Recibo_HerdaOrigemDaFatura_E_MarcaLiquidada()
    {
        // Sprint 529c: recibo (doc 300) liquida a fatura local da Reparação (externalId 100).
        // O recibo deve herdar a origem (Reparacao) e a fatura deve ficar marcada como liquidada.
        var svc = MakeService(
            localRows: new[] { Local("FT 2026/3", 13500, origem: "Reparacao", nome: "Maria", nif: "235061921", externalId: "100") },
            moloniDocs: new[] { MoloniDoc(100, "FT", gross: 13500, net: 10976, taxes: 2524, status: 1, nome: "Maria") },
            moloniReceipts: new[] { MoloniDoc(300, "RG", gross: 13500, net: 10976, taxes: 2524, status: 1, nome: "Maria", assoc: 100) });

        var res = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, null));

        var recibo = res.Items.First(d => d.TipoCodigo == "RG");
        recibo.Origem.Should().Be("Reparacao");      // herdou a origem da fatura que liquida
        recibo.ClienteNome.Should().Be("Maria");

        var fatura = res.Items.First(d => d.TipoCodigo == "FT");
        fatura.ReciboNumero.Should().NotBeNull();    // marcada liquidada → esconde "Emitir recibo"
    }
}
