using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 525: compras a fornecedores intra-UE (autoliquidação / reverse charge) NÃO podem
/// contar como IVA dedutível em PT. O IVA é liquidado e deduzido em simultâneo (efeito zero),
/// e o IVA estrangeiro nunca é dedutível cá. Estes testes provam que o cálculo do Relatório IVA
/// exclui movimentos/despesas marcados como ReverseCharge.
/// </summary>
public class RelatorioFiscalReverseChargeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTime From = DateTime.MinValue;
    private static readonly DateTime To = DateTime.MaxValue;

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"iva-rc-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(Tenant));

    [Fact]
    public async Task SumPecasCusto_ExcluiPartMovimentoReverseCharge()
    {
        await using var db = NewDb();
        var part = new Part { TenantId = Tenant, Nome = "Bateria iPhone", Sku = "BAT-1", CustoUnitarioCents = 12300 };
        db.Parts.Add(part);
        db.PartMovimentos.Add(new PartMovimento
        {
            TenantId = Tenant, Part = part, Quantidade = 1,
            Motivo = PartMovimentoMotivo.Entrada, ReverseCharge = false,
        });
        db.PartMovimentos.Add(new PartMovimento
        {
            TenantId = Tenant, Part = part, Quantidade = 2,
            Motivo = PartMovimentoMotivo.Entrada, ReverseCharge = true, // Utopya/FR — autoliquidação
        });
        await db.SaveChangesAsync();

        var repo = new RelatorioFiscalRepository(db);
        var custo = await repo.SumPecasCustoComIvaAsync(From, To);

        // Só a entrada normal (1× 12300) conta. A intra-UE (2× 12300) é excluída.
        custo.Should().Be(12300);
    }

    [Fact]
    public async Task ListComprasStock_ExcluiReverseCharge()
    {
        await using var db = NewDb();
        var part = new Part { TenantId = Tenant, Nome = "Chassis A15", Sku = "CHS-1", CustoUnitarioCents = 5000 };
        db.Parts.Add(part);
        db.PartMovimentos.Add(new PartMovimento { TenantId = Tenant, Part = part, Quantidade = 1, Motivo = PartMovimentoMotivo.Entrada, ReverseCharge = false });
        db.PartMovimentos.Add(new PartMovimento { TenantId = Tenant, Part = part, Quantidade = 1, Motivo = PartMovimentoMotivo.Entrada, ReverseCharge = true });
        await db.SaveChangesAsync();

        var repo = new RelatorioFiscalRepository(db);
        var linhas = await repo.ListComprasStockAsync(From, To);

        linhas.Should().HaveCount(1); // o detalhe bate certo com o total — só a entrada normal aparece
    }

    [Fact]
    public async Task SumPecasCusto_ExcluiDespesaReverseCharge()
    {
        await using var db = NewDb();
        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Peças Utopya", Categoria = DespesaCategoria.Pecas,
            ValorCents = 9000, Data = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            IsCogs = false, ReverseCharge = true,
        });
        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Peças Tudo4Mobile (PT)", Categoria = DespesaCategoria.Pecas,
            ValorCents = 4000, Data = new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc),
            IsCogs = false, ReverseCharge = false,
        });
        await db.SaveChangesAsync();

        var repo = new RelatorioFiscalRepository(db);
        var custo = await repo.SumPecasCustoComIvaAsync(From, To);

        // Só a despesa PT (4000) é dedutível. A intra-UE (9000) é excluída.
        custo.Should().Be(4000);
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
