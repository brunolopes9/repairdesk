using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Dashboard;

namespace RepairDesk.Tests.Dashboard;

public class DashboardKpiHojeServiceTests
{
    [Fact]
    public async Task SemDados_DevolveZeros()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var service = CreateService(db);

        var result = await service.GetAsync(new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc));

        result.ReparacoesEmCurso.Should().Be(0);
        result.ValorAReceberCents.Should().Be(0);
        result.StockCriticoCount.Should().Be(0);
        result.Receita7d.Should().HaveCount(7).And.OnlyContain(x => x == 0);
        result.ReparacoesEntregues7d.Should().Be(0);
        result.LucroEstimado7dCents.Should().Be(0);
        result.TempoMedioReparacaoHoras.Should().BeNull();
    }

    [Fact]
    public async Task ComReparacoesEmCursoEValorAReceber_CalculaHoje()
    {
        var tenantId = Guid.NewGuid();
        var hoje = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb(tenantId);
        var cliente = new Cliente { Nome = "Cliente Dashboard" };

        db.AddRange(
            cliente,
            new Reparacao
            {
                Cliente = cliente,
                ClienteId = cliente.Id,
                Numero = 1,
                Equipamento = "iPhone 13",
                Avaria = "Ecra partido",
                Estado = RepairStatus.EmReparacao,
                OrcamentoCents = 8_000,
            },
            new Reparacao
            {
                Cliente = cliente,
                ClienteId = cliente.Id,
                Numero = 2,
                Equipamento = "Samsung A52",
                Avaria = "Bateria",
                Estado = RepairStatus.Entregue,
                EstadoPagamento = PaymentStatus.NaoPago,
                EntregueEm = hoje.AddHours(11),
                PrecoFinalCents = 12_500,
            },
            new Part
            {
                Nome = "Ecra iPhone 13",
                QtdStock = 1,
                QtdMinima = 2,
                CustoUnitarioCents = 3_000,
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(hoje);

        result.ReparacoesEmCurso.Should().Be(1);
        result.ValorAReceberCents.Should().Be(12_500);
        result.StockCriticoCount.Should().Be(1);
    }

    [Fact]
    public async Task MultiTenant_NaoVeDadosDeOutroTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var hoje = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb(tenantA);

        var clienteA = new Cliente { TenantId = tenantA, Nome = "Cliente A" };
        var clienteB = new Cliente { TenantId = tenantB, Nome = "Cliente B" };

        db.AddRange(
            clienteA,
            clienteB,
            new Reparacao
            {
                TenantId = tenantA,
                Cliente = clienteA,
                ClienteId = clienteA.Id,
                Numero = 10,
                Equipamento = "iPhone A",
                Avaria = "Teste",
                Estado = RepairStatus.EmReparacao,
            },
            new Reparacao
            {
                TenantId = tenantB,
                Cliente = clienteB,
                ClienteId = clienteB.Id,
                Numero = 20,
                Equipamento = "iPhone B",
                Avaria = "Teste",
                Estado = RepairStatus.EmReparacao,
            },
            new Reparacao
            {
                TenantId = tenantB,
                Cliente = clienteB,
                ClienteId = clienteB.Id,
                Numero = 21,
                Equipamento = "MacBook B",
                Avaria = "Teste",
                Estado = RepairStatus.Entregue,
                EstadoPagamento = PaymentStatus.NaoPago,
                EntregueEm = hoje.AddHours(10),
                PrecoFinalCents = 99_999,
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(hoje);

        result.ReparacoesEmCurso.Should().Be(1);
        result.ValorAReceberCents.Should().Be(0);
    }

    private static DashboardKpiHojeService CreateService(AppDbContext db)
        => new(new DashboardRepository(db));

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard-kpis-hoje-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public bool HasTenant => true;
    }
}
