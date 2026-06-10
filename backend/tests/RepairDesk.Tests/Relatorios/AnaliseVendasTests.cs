using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 547 (Doc 93 #2): Análise de Vendas — top artigos (linhas de vendas PAGAS, agregadas
/// por descrição; margem só quando todas as linhas têm custo) + top clientes (receita das 3
/// fontes com as MESMAS condições do snapshot Negócio).
/// </summary>
public class AnaliseVendasTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dentro = new(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TopArtigos_AgregaPorDescricao_MargemSoComCusto_SoPagasNoPeriodo()
    {
        await using var db = NewDb();
        var part = new Part { TenantId = Tenant, Nome = "Película iPhone", Sku = "FILM-1", CustoUnitarioCents = 200 };
        db.Parts.Add(part);

        db.Vendas.AddRange(
            new Venda
            {
                TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = Dentro,
                Items = new List<VendaItem>
                {
                    new() { TenantId = Tenant, Descricao = "Película iPhone", Quantidade = 2, PrecoUnitarioCents = 1000, IvaRate = 23m, Part = part },
                    new() { TenantId = Tenant, Descricao = "iPhone 12 recond", Quantidade = 1, PrecoUnitarioCents = 50000, IvaRate = 23m }, // sem Part → margem null
                },
            },
            new Venda
            {
                TenantId = Tenant, Numero = 2, Status = VendaStatus.Paga, Data = Dentro,
                Items = new List<VendaItem>
                {
                    new() { TenantId = Tenant, Descricao = "película iphone", Quantidade = 3, PrecoUnitarioCents = 1000, IvaRate = 23m, Part = part }, // agrega case-insensitive
                },
            },
            new Venda // FORA: não paga
            {
                TenantId = Tenant, Numero = 3, Status = VendaStatus.Pendente, Data = Dentro,
                Items = new List<VendaItem> { new() { TenantId = Tenant, Descricao = "Película iPhone", Quantidade = 9, PrecoUnitarioCents = 1000, IvaRate = 23m } },
            });
        await db.SaveChangesAsync();

        var repo = new RelatorioNegocioRepository(db, new FixedTenant(Tenant));
        var top = await repo.GetTopArtigosAsync(From, To, 10);

        top.Should().HaveCount(2);
        top[0].Descricao.Should().Be("iPhone 12 recond"); // maior receita primeiro
        top[0].ReceitaCents.Should().Be(50000);
        top[0].MargemCents.Should().BeNull();              // sem custo registado
        top[1].Quantidade.Should().Be(5);                  // 2 + 3 agregadas (case-insensitive)
        top[1].ReceitaCents.Should().Be(5000);
        top[1].MargemCents.Should().Be(5000 - 5 * 200);    // margem com custo
    }

    [Fact]
    public async Task TopClientes_SomaTresFontes_MesmasCondicoesDoSnapshot()
    {
        await using var db = NewDb();
        var anaId = Guid.NewGuid();
        var ruiId = Guid.NewGuid();
        db.Clientes.AddRange(
            new Cliente { Id = anaId, TenantId = Tenant, Nome = "Ana", Telefone = "910000001" },
            new Cliente { Id = ruiId, TenantId = Tenant, Nome = "Rui", Telefone = "910000002" });

        // Ana: reparação paga (100€) + venda paga (50€) = 150€, 2 docs.
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = Tenant, Numero = 1, ClienteId = anaId, Equipamento = "iPhone", Avaria = "Ecrã",
            Estado = RepairStatus.Entregue, EntregueEm = Dentro, EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 10000,
        });
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = Dentro, ClienteId = anaId,
            Items = new List<VendaItem> { new() { TenantId = Tenant, Descricao = "Capa", Quantidade = 1, PrecoUnitarioCents = 5000, IvaRate = 23m } },
        });
        // Rui: trabalho concluído pago (300€).
        db.Trabalhos.Add(new Trabalho
        {
            TenantId = Tenant, Numero = 1, Titulo = "Website", ClienteId = ruiId,
            Status = TrabalhoStatus.Concluido, DataConclusao = Dentro, EstadoPagamento = PaymentStatus.Pago,
            PrecoFinalCents = 30000,
        });
        // FORA: reparação da Ana entregue mas NÃO paga.
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = Tenant, Numero = 2, ClienteId = anaId, Equipamento = "iPad", Avaria = "Bateria",
            Estado = RepairStatus.Entregue, EntregueEm = Dentro, EstadoPagamento = PaymentStatus.NaoPago,
            PrecoFinalCents = 99900,
        });
        await db.SaveChangesAsync();

        var repo = new RelatorioNegocioRepository(db, new FixedTenant(Tenant));
        var top = await repo.GetTopClientesAsync(From, To, 10);

        top.Should().HaveCount(2);
        top[0].Nome.Should().Be("Rui");
        top[0].ReceitaCents.Should().Be(30000);
        top[1].Nome.Should().Be("Ana");
        top[1].ReceitaCents.Should().Be(15000);
        top[1].Documentos.Should().Be(2);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"analise-vendas-{Guid.NewGuid():N}")
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
