using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Fornecedores;

/// <summary>
/// Sprint 548 (Doc 93 #3): histórico consolidado de um fornecedor — compras de stock (match por
/// Part.Fornecedor, como o Top Fornecedores do Negócio), despesas, importações (com últimas N e
/// pendentes), última compra e taxa de defeito 12m. Tudo o que estava espalhado, numa vista.
/// </summary>
public class FornecedorHistoricoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Historico_AgregaComprasDespesasImportsEDefeito()
    {
        await using var db = NewDb();
        var fornecedor = new Fornecedor { TenantId = Tenant, Name = "Tudo4Mobile", RegimeIva = RepairDesk.Core.Enums.RegimeIvaFornecedor.Nacional };
        var outro = new Fornecedor { TenantId = Tenant, Name = "Molano" };
        db.Fornecedores.AddRange(fornecedor, outro);

        // Compras de stock (lotes): 1×40 € + 2×10 € deste fornecedor + 1×90 € de OUTRO (fora).
        var doc = new CompraDocumento { TenantId = Tenant, Fornecedor = fornecedor, Data = DateTime.UtcNow.AddMonths(-4), NumeroFatura = "FT 99" };
        var ecra = new CompraLinha { TenantId = Tenant, Descricao = "Ecrã iPhone", Quantidade = 1, PrecoUnitarioPago = 40m, TaxaIvaCompra = 0.23m };
        doc.Linhas.Add(ecra);
        doc.Linhas.Add(new CompraLinha { TenantId = Tenant, Descricao = "Bateria", Quantidade = 2, PrecoUnitarioPago = 10m, TaxaIvaCompra = 0.23m });
        var docOutro = new CompraDocumento { TenantId = Tenant, Fornecedor = outro, Data = DateTime.UtcNow.AddMonths(-4), NumeroFatura = "X 1" };
        docOutro.Linhas.Add(new CompraLinha { TenantId = Tenant, Descricao = "Chassis", Quantidade = 1, PrecoUnitarioPago = 90m, TaxaIvaCompra = 0.23m });
        db.ComprasDocumentos.AddRange(doc, docOutro);

        // Despesa deste fornecedor (porte) + COGS (fora) + de outro fornecedor (fora).
        db.Despesas.AddRange(
            new Despesa { TenantId = Tenant, Descricao = "Portes", Categoria = DespesaCategoria.Transporte, ValorCents = 500, Fornecedor = "Tudo4Mobile" },
            new Despesa { TenantId = Tenant, Descricao = "COGS", Categoria = DespesaCategoria.Pecas, ValorCents = 999, Fornecedor = "Tudo4Mobile", IsCogs = true },
            new Despesa { TenantId = Tenant, Descricao = "Outra", Categoria = DespesaCategoria.Transporte, ValorCents = 777, Fornecedor = "Molano" });

        // Imports: 1 aprovada antiga + 1 pendente recente.
        db.SupplierInvoiceImports.AddRange(
            new SupplierInvoiceImport
            {
                TenantId = Tenant, Fornecedor = fornecedor, PdfSha256 = new string('a', 64),
                PdfRelativePath = "a.pdf", ParsedDocumentNumber = "FT 100", ParsedTotalCents = 4000,
                ParsedDocumentDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = SupplierInvoiceImportStatus.Approved,
            },
            new SupplierInvoiceImport
            {
                TenantId = Tenant, Fornecedor = fornecedor, PdfSha256 = new string('b', 64),
                PdfRelativePath = "b.pdf", ParsedDocumentNumber = "FT 101", ParsedTotalCents = 2000,
                ParsedDocumentDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = SupplierInvoiceImportStatus.Pending,
            });

        // Vendido nos últimos 12 meses: 1 ecrã deste fornecedor (via lote).
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Estado = VendaEstado.Entregue, Data = DateTime.UtcNow.AddMonths(-3),
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "Ecrã iPhone", Quantidade = 1, PrecoUnitarioCents = 6000, IvaRate = 23m, CompraLinha = ecra },
            },
        });
        await db.SaveChangesAsync();

        var repo = new FornecedorRepository(db);
        var h = await repo.GetHistoricoAsync(fornecedor.Id);

        h.Should().NotBeNull();
        h!.Nome.Should().Be("Tudo4Mobile");
        h.ComprasStockCents.Should().Be(4000 + 2 * 1000);   // só lotes deste fornecedor
        h.DespesasCents.Should().Be(500);                    // COGS e outros fornecedores fora
        h.ImportsTotal.Should().Be(2);
        h.ImportsPendentes.Should().Be(1);
        h.UltimaCompraEm.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        h.ItensVendidos12m.Should().Be(1);
        h.ItensComReparacao12m.Should().Be(0);
        h.TaxaDefeitoPct12m.Should().Be(0m);
        h.UltimasFaturas.Should().HaveCount(2);
        h.UltimasFaturas[0].Numero.Should().Be("FT 101");    // mais recente primeiro
    }

    [Fact]
    public async Task Historico_FornecedorInexistente_DevolveNull()
    {
        await using var db = NewDb();
        var repo = new FornecedorRepository(db);
        (await repo.GetHistoricoAsync(Guid.NewGuid())).Should().BeNull();
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"fornecedor-hist-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(Tenant));

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
