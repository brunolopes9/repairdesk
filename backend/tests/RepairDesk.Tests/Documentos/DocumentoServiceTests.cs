using FluentAssertions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Documentos;

namespace RepairDesk.Tests.Documentos;

public class DocumentoServiceTests
{
    private sealed class FakeRepo(IReadOnlyList<DocumentoVendaRow> rows) : IRelatorioFiscalRepository
    {
        public Task<IReadOnlyList<DocumentoVendaRow>> ListVendaDocumentosDetalheAsync(DateTime f, DateTime t, CancellationToken ct = default)
            => Task.FromResult(rows);

        // Membros não usados por este serviço.
        public Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearInvoiceFieldsAsync(string tipo, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SumPecasCustoComIvaAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SumDespesasComIvaAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IvaDeducaoLinha>> ListComprasStockAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IvaDeducaoLinha>> ListDespesasOpExAsync(DateTime f, DateTime t, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static DocumentoVendaRow Row(string? numero, int total, string origem = "Venda", string? nif = null, string? nome = null)
        => new(Guid.NewGuid(), origem, 1, numero, "100", "http://pdf", BillingProvider.Moloni, DateTime.UtcNow, Guid.NewGuid(), nome, nif, total);

    [Fact]
    public async Task ListVendas_DerivaTipo_ExtraiIva_SomaTotais()
    {
        var svc = new DocumentoService(new FakeRepo(new[]
        {
            Row("FT M/2", 13500, nome: "Maria", nif: "235061921"),
            Row("FS 2026/7", 1000),
        }));

        var res = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, null));

        res.TotalDocumentos.Should().Be(2);
        res.TotalCents.Should().Be(14500);
        res.Items.Should().Contain(d => d.TipoCodigo == "FT" && d.Tipo == "Fatura");
        res.Items.Should().Contain(d => d.TipoCodigo == "FS" && d.Tipo == "Fatura Simplificada");

        // IVA 23% embutido: 135,00€ -> base 109,76€ + IVA 25,24€ (igual à fatura real da Maria).
        var ft = res.Items.First(d => d.TipoCodigo == "FT");
        ft.BaseCents.Should().Be(10976);
        ft.IvaCents.Should().Be(2524);
        res.TotalIvaCents.Should().Be(ft.IvaCents + res.Items.First(d => d.TipoCodigo == "FS").IvaCents);
    }

    [Fact]
    public async Task ListVendas_FiltraPorTipo_E_Por_Q()
    {
        var svc = new DocumentoService(new FakeRepo(new[]
        {
            Row("FT M/2", 13500, nome: "Maria", nif: "235061921"),
            Row("FS 2026/7", 1000, nome: "Joao"),
        }));

        var soFt = await svc.ListVendasAsync(new DocumentosFiltro(null, null, null, "FT"));
        soFt.TotalDocumentos.Should().Be(1);
        soFt.Items[0].TipoCodigo.Should().Be("FT");

        var porNif = await svc.ListVendasAsync(new DocumentosFiltro(null, null, "235061921", null));
        porNif.TotalDocumentos.Should().Be(1);
        porNif.Items[0].ClienteNome.Should().Be("Maria");
    }

    [Fact]
    public async Task ExportCsv_TemCabecalhoEUmaLinhaPorDocumento()
    {
        var svc = new DocumentoService(new FakeRepo(new[] { Row("FT M/2", 13500, nome: "Maria", nif: "235061921") }));
        var bytes = await svc.ExportVendasCsvAsync(null, null);
        var csv = System.Text.Encoding.UTF8.GetString(bytes);

        csv.Should().Contain("data");
        csv.Should().Contain("total_eur");
        csv.Should().Contain("FT M/2");
        csv.Should().Contain("235061921");
    }
}
