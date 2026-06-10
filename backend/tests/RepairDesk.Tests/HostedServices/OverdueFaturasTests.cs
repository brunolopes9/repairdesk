using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.HostedServices;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.HostedServices;

/// <summary>
/// Sprint 545: o cron de cobranças passa a avisar também a DÍVIDA FORMAL — Faturas a crédito (FT)
/// emitidas há +N dias e ainda sem Recibo de liquidação (mesma semântica do KPI "Em dívida" S544).
/// Cobre Reparações + Trabalhos + Vendas; FS/FR (pagamento imediato) e FT já liquidadas ficam fora.
/// </summary>
public class OverdueFaturasTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public async Task ContaSoFtAtivasSemRecibo_AntesDoCutoff_AgrupadasPorTenant()
    {
        await using var db = NewDb();
        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = TenantA, Nome = "Pátio Fidalgo", Telefone = "910000000" });

        var antiga = DateTime.UtcNow.AddDays(-45);
        var recente = DateTime.UtcNow.AddDays(-5);

        // ENTRA: reparação com FT antiga sem recibo (120€).
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = TenantA, Numero = 1, ClienteId = clienteId, Equipamento = "iPhone", Avaria = "Ecrã",
            PrecoFinalCents = 12000, InvoiceNumber = "FT 2026/1", InvoiceEmittedAt = antiga,
        });
        // ENTRA: venda com FT antiga sem recibo (300€ em items).
        db.Vendas.Add(new Venda
        {
            TenantId = TenantA, Numero = 1, InvoiceNumber = "FT 2026/2", InvoiceEmittedAt = antiga,
            Items = new List<VendaItem>
            {
                new() { TenantId = TenantA, Descricao = "iPhone 11", Quantidade = 1, PrecoUnitarioCents = 30000, IvaRate = 23m },
            },
        });
        // FORA: FT recente (ainda dentro do prazo).
        db.Trabalhos.Add(new Trabalho
        {
            TenantId = TenantA, Numero = 1, Titulo = "Website",
            PrecoFinalCents = 50000, InvoiceNumber = "FT 2026/3", InvoiceEmittedAt = recente,
        });
        // FORA: FT antiga JÁ liquidada por recibo.
        db.Trabalhos.Add(new Trabalho
        {
            TenantId = TenantA, Numero = 2, Titulo = "App",
            PrecoFinalCents = 80000, InvoiceNumber = "FT 2026/4", InvoiceEmittedAt = antiga,
            ReciboNumero = "RG 2026/1", ReciboEmitidoEm = DateTime.UtcNow.AddDays(-2),
        });
        // FORA: FS (pagamento imediato — nunca é dívida).
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = TenantA, Numero = 2, ClienteId = clienteId, Equipamento = "Samsung", Avaria = "Bateria",
            PrecoFinalCents = 9000, InvoiceNumber = "FS 2026/9", InvoiceEmittedAt = antiga,
        });
        // ENTRA (TENANT B): isolamento por tenant no merge.
        db.Trabalhos.Add(new Trabalho
        {
            TenantId = TenantB, Numero = 1, Titulo = "Logo",
            PrecoFinalCents = 15000, InvoiceNumber = "FT 2026/7", InvoiceEmittedAt = antiga,
        });
        await db.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var result = await OverdueInvoicesHostedService.CountFaturasEmDividaPorTenantAsync(db, cutoff);

        result.Should().HaveCount(2);
        result[TenantA].Count.Should().Be(2);                  // reparação + venda
        result[TenantA].TotalCents.Should().Be(12000 + 30000);
        result[TenantB].Count.Should().Be(1);
        result[TenantB].TotalCents.Should().Be(15000);
    }

    [Fact]
    public async Task SemDivida_DevolveVazio()
    {
        await using var db = NewDb();
        var result = await OverdueInvoicesHostedService.CountFaturasEmDividaPorTenantAsync(
            db, DateTime.UtcNow.AddDays(-30));
        result.Should().BeEmpty();
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"overdue-ft-{Guid.NewGuid():N}")
                .Options,
            new NoTenant());

    /// <summary>Cron corre SEM tenant no contexto (IgnoreQueryFilters) — espelha o ambiente real.</summary>
    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
    }
}
