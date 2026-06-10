using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documentos;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 542: Extrato unificado (Vendas + Compras + Despesas) com export PDF para o contabilista.
/// Regras de totais explícitas: NC subtrai, RG (liquidação) e ORC não são faturação, Anulados e
/// Rascunhos listam mas não somam. Compras/Despesas reutilizam as linhas do relatório IVA.
/// </summary>
public class ExtratoServiceTests
{
    static ExtratoServiceTests()
    {
        // O Program.cs define a licença no arranque da API; nos testes definimos aqui.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void ComputeTotais_NCSubtrai_RGeORCnaoSomam_AnuladoNaoSoma()
    {
        var vendas = new List<DocumentoDto>
        {
            Doc("FT", 12300, 2300, "Ativo"),
            Doc("NC", 2300, 430, "Ativo"),     // subtrai
            Doc("RG", 12300, 0, "Ativo"),      // liquidação — não é faturação
            Doc("FS", 5000, 935, "Anulado"),   // anulado — lista mas não soma
            Doc("FR", 6150, 1150, "Ativo"),
        };
        var compras = new List<IvaDeducaoLinha>
        {
            new(DateTime.UtcNow, "Ecrã iPhone", "Tudo4Mobile", "stock-entrada", 4000, 748),
        };
        var despesas = new List<IvaDeducaoLinha>
        {
            new(DateTime.UtcNow, "Renda", null, "despesa-opex", 30000, 5610),
        };

        var t = ExtratoService.ComputeTotais(vendas, compras, despesas);

        t.FaturadoCents.Should().Be(12300 - 2300 + 6150); // 16150
        t.IvaVendasCents.Should().Be(2300 - 430 + 1150);  // 3020
        t.ComprasCents.Should().Be(4000);
        t.DespesasCents.Should().Be(30000);
        t.ResultadoCents.Should().Be(16150 - 4000 - 30000);
    }

    [Fact]
    public async Task ExportPdf_GeraPdfComAsTresSeccoes()
    {
        await using var db = NewDb();
        db.Tenants.Add(new Tenant { Id = TenantId, Name = "LopesTech", Nif = "123456789" });
        // compra de stock no período (entra via ListComprasStockAsync)
        var part = new Part { TenantId = TenantId, Nome = "Ecrã A15", Sku = "ECR-A15", CustoUnitarioCents = 4000 };
        db.Parts.Add(part);
        db.PartMovimentos.Add(new PartMovimento { TenantId = TenantId, Part = part, Quantidade = 1, Motivo = PartMovimentoMotivo.Entrada });
        // despesa OpEx no período
        db.Despesas.Add(new Despesa { TenantId = TenantId, Descricao = "Renda Junho", Categoria = DespesaCategoria.Renda, ValorCents = 30000, Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var vendas = new DocumentosListDto(
            new List<DocumentoDto> { Doc("FT", 12300, 2300, "Ativo") }, 1, 12300, 2300, 10000);
        var service = new ExtratoService(
            new FakeDocumentos(vendas),
            new RelatorioFiscalRepository(db),
            new TenantRepository(db),
            new FixedTenant(TenantId));

        var (pdf, filename) = await service.ExportPdfAsync(
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1));

        pdf.Should().NotBeNullOrEmpty();
        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        filename.Should().StartWith("extrato_").And.EndWith(".pdf");
    }

    [Fact]
    public async Task ExportPdf_PeriodoInvalido_Recusa()
    {
        await using var db = NewDb();
        var service = new ExtratoService(
            new FakeDocumentos(new DocumentosListDto(Array.Empty<DocumentoDto>(), 0, 0, 0, 0)),
            new RelatorioFiscalRepository(db),
            new TenantRepository(db),
            new FixedTenant(TenantId));

        var act = () => service.ExportPdfAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1));

        (await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ValidationException>())
            .Which.Code.Should().Be("invalid_period");
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"extrato-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(TenantId));

    private static DocumentoDto Doc(string tipoCodigo, int totalCents, int ivaCents, string estado) =>
        new(Guid.NewGuid(), "Moloni", 0, DocumentoTipo.FromCodigo(tipoCodigo).Nome, tipoCodigo,
            $"{tipoCodigo} 2026/1", "1", null, "Moloni", DateTime.UtcNow, null, "Cliente X", null,
            totalCents, ivaCents, totalCents - ivaCents, estado);

    private sealed class FakeDocumentos : IDocumentoService
    {
        private readonly DocumentosListDto _vendas;
        public FakeDocumentos(DocumentosListDto vendas) => _vendas = vendas;
        public Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default)
            => Task.FromResult(_vendas);
        public Task<byte[]> ExportVendasCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ReciboResultDto> EmitirReciboAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
