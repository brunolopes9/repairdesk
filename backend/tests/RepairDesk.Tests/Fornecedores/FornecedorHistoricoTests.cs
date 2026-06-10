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
        var fornecedor = new Fornecedor { TenantId = Tenant, Name = "Tudo4Mobile", IntraUe = false };
        var outro = new Fornecedor { TenantId = Tenant, Name = "Molano" };
        db.Fornecedores.AddRange(fornecedor, outro);

        // Compras de stock: 2 entradas deste fornecedor (1×4000 + 2×1000) + 1 de OUTRO (fora).
        var p1 = new Part { TenantId = Tenant, Nome = "Ecrã iPhone", Sku = "E1", CustoUnitarioCents = 4000, Fornecedor = "Tudo4Mobile" };
        var p2 = new Part { TenantId = Tenant, Nome = "Bateria", Sku = "B1", CustoUnitarioCents = 1000, Fornecedor = "Tudo4Mobile" };
        var p3 = new Part { TenantId = Tenant, Nome = "Chassis", Sku = "C1", CustoUnitarioCents = 9000, Fornecedor = "Molano" };
        db.Parts.AddRange(p1, p2, p3);
        db.PartMovimentos.AddRange(
            new PartMovimento { TenantId = Tenant, Part = p1, Quantidade = 1, Motivo = PartMovimentoMotivo.Entrada },
            new PartMovimento { TenantId = Tenant, Part = p2, Quantidade = 2, Motivo = PartMovimentoMotivo.Entrada },
            new PartMovimento { TenantId = Tenant, Part = p3, Quantidade = 1, Motivo = PartMovimentoMotivo.Entrada });

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

        // Defeito 12m: 2 IMEIs vendidos deste fornecedor; 1 voltou em reparação DEPOIS da venda.
        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = Tenant, Nome = "Ana", Telefone = "910000000" });
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = DateTime.UtcNow.AddMonths(-3),
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "iPhone A", Quantidade = 1, PrecoUnitarioCents = 30000, IvaRate = 23m, FornecedorNome = "Tudo4Mobile", Imei = "111111111111119" },
                new() { TenantId = Tenant, Descricao = "iPhone B", Quantidade = 1, PrecoUnitarioCents = 30000, IvaRate = 23m, FornecedorNome = "Tudo4Mobile", Imei = "222222222222226" },
            },
        });
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = Tenant, Numero = 1, ClienteId = clienteId, Equipamento = "iPhone A", Avaria = "Ecrã",
            Imei = "111111111111119",
        });
        await db.SaveChangesAsync();

        var repo = new FornecedorRepository(db);
        var h = await repo.GetHistoricoAsync(fornecedor.Id);

        h.Should().NotBeNull();
        h!.Nome.Should().Be("Tudo4Mobile");
        h.ComprasStockCents.Should().Be(4000 + 2 * 1000);   // só entradas deste fornecedor
        h.DespesasCents.Should().Be(500);                    // COGS e outros fornecedores fora
        h.ImportsTotal.Should().Be(2);
        h.ImportsPendentes.Should().Be(1);
        h.UltimaCompraEm.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        h.ItensVendidos12m.Should().Be(2);
        h.ItensComReparacao12m.Should().Be(1);
        h.TaxaDefeitoPct12m.Should().Be(50m);
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
