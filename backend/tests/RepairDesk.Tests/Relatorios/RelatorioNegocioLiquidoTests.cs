using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 550 (mão de contabilista): o Dashboard Negócio passa a calcular margens sobre a
/// receita LÍQUIDA de IVA (o IVA liquidado é do Estado) e o lucro bruto subtrai também o
/// custo dos artigos vendidos (antes só saíam as peças das reparações — um telemóvel de
/// 250€ vendido a 300€ aparecia como +300€ de lucro). IVA das vendas por linha (taxa da
/// linha; regime da margem = IVA sobre a margem quando há custo), serviços a 23/123.
/// </summary>
public class RelatorioNegocioLiquidoTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task MargensSobreReceitaLiquida_ComCogsDasVendas()
    {
        await using var db = NewDb();
        var agora = DateTime.UtcNow;

        // Reparação paga 123,00 € com peça de 30,00 € (movimento UsoEmReparacao, CreatedAt=now).
        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = Tenant, Nome = "Ana", Telefone = "910000000" });
        var reparacao = new Reparacao
        {
            TenantId = Tenant, Numero = 1, ClienteId = clienteId, Equipamento = "iPhone", Avaria = "Ecrã",
            EntregueEm = agora, PrecoFinalCents = 12300, EstadoPagamento = PaymentStatus.Pago,
        };
        var peca = new Part { TenantId = Tenant, Nome = "Ecrã", Sku = "E1", CustoUnitarioCents = 3000 };
        db.Reparacoes.Add(reparacao);
        db.Parts.Add(peca);
        db.PartMovimentos.Add(new PartMovimento
        {
            TenantId = Tenant, Part = peca, Quantidade = -1,
            Motivo = PartMovimentoMotivo.UsoEmReparacao, Reparacao = reparacao,
        });

        // Venda paga mista: linha normal 123,00 € @23% (custo 50,00 €) + linha regime da margem
        // 300,00 € Recondicionado (custo 250,00 € → IVA sobre a margem de 50,00 €).
        var partNormal = new Part { TenantId = Tenant, Nome = "Capa", Sku = "C1", CustoUnitarioCents = 5000 };
        var partMargem = new Part { TenantId = Tenant, Nome = "iPhone 12", Sku = "IP12", CustoUnitarioCents = 25000 };
        db.Parts.AddRange(partNormal, partMargem);
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = agora, TotalCents = 42300,
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "Capa", Quantidade = 1, PrecoUnitarioCents = 12300, IvaRate = 23m, Condicao = CondicaoArtigo.NaoAplicavel, Part = partNormal },
                new() { TenantId = Tenant, Descricao = "iPhone 12", Quantidade = 1, PrecoUnitarioCents = 30000, IvaRate = 0m, Condicao = CondicaoArtigo.Recondicionado, Part = partMargem },
            },
        });

        // Venda paga SEM linhas (dados antigos): fallback 23/123 sobre o total 12,30 €.
        db.Vendas.Add(new Venda { TenantId = Tenant, Numero = 2, Status = VendaStatus.Paga, Data = agora, TotalCents = 1230 });

        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Renda", Categoria = DespesaCategoria.Servicos,
            ValorCents = 1000, Data = agora, IsCogs = false,
        });
        await db.SaveChangesAsync();

        var service = NewService(db);
        var r = await service.GetAsync(agora.Year, (agora.Month - 1) / 3 + 1);

        r.ReceitaTotalCents.Should().Be(12300 + 42300 + 1230);              // 55830 (c/ IVA)
        // IVA: serviços 12300×23/123=2300 · linha normal 2300 · margem (30000−25000)×23/123=935 · fallback 230.
        r.IvaEmbutidoCents.Should().Be(2300 + 2300 + 935 + 230);            // 5765
        r.ReceitaLiquidaCents.Should().Be(55830 - 5765);                    // 50065
        r.CustoPecasCents.Should().Be(3000);
        r.CustoVendasCents.Should().Be(5000 + 25000);                       // COGS das vendas entra agora
        r.LucroBrutoCents.Should().Be(50065 - 3000 - 30000);                // 17065
        r.MargemMedia.Should().Be(34.09m);                                  // 17065/50065
        r.LucroOperacionalCents.Should().Be(17065 - 1000);
        r.MargemOperacionalPct.Should().Be(32.09m);                         // 16065/50065
    }

    [Fact]
    public async Task RegimeMargemSemCusto_NaoEstimaIvaNemCogs()
    {
        await using var db = NewDb();
        var agora = DateTime.UtcNow;

        // 2ª mão sem Part associado: IVA da margem não é estimável → contribui 0 (mesmo
        // tratamento do relatório IVA) e não há custo a subtrair.
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = agora, TotalCents = 30000,
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "Samsung usado", Quantidade = 1, PrecoUnitarioCents = 30000, IvaRate = 0m, Condicao = CondicaoArtigo.Usado },
            },
        });
        await db.SaveChangesAsync();

        var service = NewService(db);
        var r = await service.GetAsync(agora.Year, (agora.Month - 1) / 3 + 1);

        r.IvaEmbutidoCents.Should().Be(0);
        r.CustoVendasCents.Should().Be(0);
        r.ReceitaLiquidaCents.Should().Be(30000);
        r.LucroBrutoCents.Should().Be(30000);
    }

    private static RelatorioNegocioService NewService(AppDbContext db)
    {
        var tenant = new FixedTenant(Tenant);
        return new RelatorioNegocioService(new RelatorioNegocioRepository(db, tenant), tenant);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"negocio-liquido-{Guid.NewGuid():N}")
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
