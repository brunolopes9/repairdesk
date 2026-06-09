using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 535: regime da margem — bens em segunda mão (CIVA art. 308.º / M13). O IVA incide sobre
/// a margem (venda − compra), não sobre o total. Estes testes provam o apuramento da BASE da margem
/// no Relatório IVA: só artigos 2ª mão com custo, margem negativa clampa a 0, linhas sem custo são
/// contadas à parte (não inflam a base), e respeita o período de faturação.
/// </summary>
public class RelatorioMargemRegimeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"iva-margem-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(Tenant));

    [Fact]
    public async Task SumMargemRegime_SoSegundaMaoComCusto_ClampaNegativa_ContaSemCusto()
    {
        await using var db = NewDb();
        var partLucro = new Part { TenantId = Tenant, Nome = "iPhone 12 recond", Sku = "IP12-R", CustoUnitarioCents = 30000 };
        var partPrejuizo = new Part { TenantId = Tenant, Nome = "iPhone 11 recond", Sku = "IP11-R", CustoUnitarioCents = 25000 };
        db.Parts.AddRange(partLucro, partPrejuizo);

        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga,
            InvoiceExternalId = "100", InvoiceEmittedAt = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            Items = new List<VendaItem>
            {
                // recondicionado com lucro: 50000 − 30000 = 20000 de margem
                new() { TenantId = Tenant, Descricao = "iPhone 12", Quantidade = 1, PrecoUnitarioCents = 50000, IvaRate = 23m, Condicao = CondicaoArtigo.Recondicionado, Part = partLucro },
                // usado abaixo do custo: 20000 − 25000 = −5000 → clampa a 0
                new() { TenantId = Tenant, Descricao = "iPhone 11", Quantidade = 1, PrecoUnitarioCents = 20000, IvaRate = 23m, Condicao = CondicaoArtigo.Usado, Part = partPrejuizo },
                // novo (não é 2ª mão) → ignorado
                new() { TenantId = Tenant, Descricao = "Capa nova", Quantidade = 1, PrecoUnitarioCents = 1500, IvaRate = 23m, Condicao = CondicaoArtigo.Novo },
                // 2ª mão SEM Part/custo → conta como semCusto, não entra na base
                new() { TenantId = Tenant, Descricao = "Telemóvel usado avulso", Quantidade = 1, PrecoUnitarioCents = 8000, IvaRate = 23m, Condicao = CondicaoArtigo.Usado },
            },
        });
        await db.SaveChangesAsync();

        var repo = new RelatorioFiscalRepository(db);
        var res = await repo.SumMargemRegimeAsync(From, To);

        res.MargemTributavelCents.Should().Be(20000); // só a margem positiva entra
        res.LinhasSemCustoCount.Should().Be(1);        // o usado avulso sem Part
    }

    [Fact]
    public async Task SumMargemRegime_ForaDoPeriodo_NaoConta()
    {
        await using var db = NewDb();
        var part = new Part { TenantId = Tenant, Nome = "iPhone", Sku = "IP-X", CustoUnitarioCents = 10000 };
        db.Parts.Add(part);
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 2, Status = VendaStatus.Paga,
            InvoiceExternalId = "101", InvoiceEmittedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), // fora do trimestre
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "iPhone", Quantidade = 1, PrecoUnitarioCents = 20000, IvaRate = 23m, Condicao = CondicaoArtigo.Recondicionado, Part = part },
            },
        });
        await db.SaveChangesAsync();

        var repo = new RelatorioFiscalRepository(db);
        var res = await repo.SumMargemRegimeAsync(From, To);

        res.MargemTributavelCents.Should().Be(0);
        res.LinhasSemCustoCount.Should().Be(0);
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
