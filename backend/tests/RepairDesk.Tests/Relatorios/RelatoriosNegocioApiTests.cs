using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Relatorios;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Relatorios;

public class RelatoriosNegocioApiTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Prefix = "Negocio Test";
    private readonly RepairDeskApiFactory _factory;

    public RelatoriosNegocioApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SemReceita_DevolveZeros()
    {
        await CleanupPeriodAsync(RepairDeskApiFactory.TenantId, 2099, 4);
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioNegocioResponse>("/api/relatorios/negocio?ano=2099&trimestre=4");

        report!.ReceitaTotalCents.Should().Be(0);
        report.LucroBrutoCents.Should().Be(0);
        report.MargemMedia.Should().Be(0);
        report.TicketMedioCents.Should().Be(0);
        report.TopReparacoesLucrativas.Should().BeEmpty();
        report.TopPecasUsadas.Should().BeEmpty();
        report.TopFornecedores.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculaReceitaCustosLucroMargemETicket()
    {
        await CleanupPeriodAsync(RepairDeskApiFactory.TenantId, 2032, 1);
        await SeedBaseScenarioAsync(RepairDeskApiFactory.TenantId, 2032, 1);
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioNegocioResponse>("/api/relatorios/negocio?ano=2032&trimestre=1");

        report!.ReceitaTotalCents.Should().Be(22_000);
        report.ReceitaReparacoesCents.Should().Be(10_000);
        report.ReceitaTrabalhosCents.Should().Be(5_000);
        report.ReceitaVendasCents.Should().Be(7_000);
        report.CustoPecasCents.Should().Be(2_000);
        report.OpexCents.Should().Be(3_000);
        report.LucroBrutoCents.Should().Be(17_000);
        report.MargemMedia.Should().Be(77.27m);
        report.TicketMedioCents.Should().Be(22_000);
    }

    [Fact]
    public async Task LucroNegativo_PreservaMargemNegativa()
    {
        await CleanupPeriodAsync(RepairDeskApiFactory.TenantId, 2033, 1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var date = new DateTime(2033, 2, 10, 10, 0, 0, DateTimeKind.Utc);
            var cliente = new Cliente { TenantId = RepairDeskApiFactory.TenantId, Nome = $"{Prefix} Cliente Negativo", Telefone = "910000000" };
            var reparacao = new Reparacao
            {
                TenantId = RepairDeskApiFactory.TenantId,
                Numero = 3301,
                Cliente = cliente,
                ClienteId = cliente.Id,
                Equipamento = $"{Prefix} Negativo",
                Avaria = "Teste",
                EntregueEm = date,
                PrecoFinalCents = 1_000,
                EstadoPagamento = PaymentStatus.Pago,
            };
            var part = new Part { TenantId = RepairDeskApiFactory.TenantId, Nome = $"{Prefix} Peca Cara", QtdStock = 1, CustoUnitarioCents = 2_000 };
            var movimento = new PartMovimento
            {
                TenantId = RepairDeskApiFactory.TenantId,
                Part = part,
                PartId = part.Id,
                Quantidade = -1,
                StockAntes = 1,
                StockDepois = 0,
                Motivo = PartMovimentoMotivo.UsoEmReparacao,
                Reparacao = reparacao,
                ReparacaoId = reparacao.Id,
                Notas = Prefix,
            };
            db.AddRange(cliente, reparacao, part, movimento, new Despesa
            {
                TenantId = RepairDeskApiFactory.TenantId,
                Descricao = $"{Prefix} OpEx Negativo",
                Categoria = DespesaCategoria.Servicos,
                ValorCents = 500,
                Data = date,
                IsCogs = false,
            });
            await db.SaveChangesAsync();
            movimento.CreatedAt = date;
            await db.SaveChangesAsync();
        }
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioNegocioResponse>("/api/relatorios/negocio?ano=2033&trimestre=1");

        report!.ReceitaTotalCents.Should().Be(1_000);
        report.CustoPecasCents.Should().Be(2_000);
        report.OpexCents.Should().Be(500);
        report.LucroBrutoCents.Should().Be(-1_500);
        report.MargemMedia.Should().Be(-150m);
    }

    [Fact]
    public async Task VendasPendentes_NaoEntramNaReceita()
    {
        await CleanupPeriodAsync(RepairDeskApiFactory.TenantId, 2034, 1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var date = new DateTime(2034, 1, 20, 12, 0, 0, DateTimeKind.Utc);
            db.Vendas.AddRange(
                new Venda
                {
                    TenantId = RepairDeskApiFactory.TenantId,
                    Numero = 3401,
                    Data = date,
                    TotalCents = 99_999,
                    Status = VendaStatus.Pendente,
                },
                new Venda
                {
                    TenantId = RepairDeskApiFactory.TenantId,
                    Numero = 3402,
                    Data = date,
                    TotalCents = 1_500,
                    Status = VendaStatus.Paga,
                });
            await db.SaveChangesAsync();
        }
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioNegocioResponse>("/api/relatorios/negocio?ano=2034&trimestre=1");

        report!.ReceitaVendasCents.Should().Be(1_500);
        report.ReceitaTotalCents.Should().Be(1_500);
    }

    [Fact]
    public async Task Tops_OrdenamPorLucroConsumoFornecedorEIsolamTenant()
    {
        await CleanupPeriodAsync(RepairDeskApiFactory.TenantId, 2035, 1);
        await CleanupPeriodAsync(RepairDeskApiFactory.SecondTenantId, 2035, 1);
        await SeedTopScenarioAsync(RepairDeskApiFactory.TenantId, 2035);
        await SeedOtherTenantNoiseAsync(RepairDeskApiFactory.SecondTenantId, 2035);
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioNegocioResponse>("/api/relatorios/negocio?ano=2035&trimestre=1");

        report!.TopReparacoesLucrativas.Should().HaveCount(2);
        report.TopReparacoesLucrativas[0].Numero.Should().Be(3501);
        report.TopReparacoesLucrativas[0].LucroCents.Should().Be(9_000);
        report.TopPecasUsadas.Select(p => p.Nome).Should().ContainInOrder($"{Prefix} Ecra", $"{Prefix} Bateria");
        report.TopPecasUsadas[0].Quantidade.Should().Be(3);
        report.TopFornecedores.Select(f => f.Nome).Should().ContainInOrder("Fornecedor A", "Fornecedor B");
        report.TopFornecedores[0].TotalCompradoCents.Should().Be(5_000);
        report.ReceitaTotalCents.Should().Be(15_000);
    }

    private async Task SeedBaseScenarioAsync(Guid tenantId, int ano, int trimestre)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var date = new DateTime(ano, (trimestre - 1) * 3 + 2, 10, 12, 0, 0, DateTimeKind.Utc);
        var cliente = new Cliente { TenantId = tenantId, Nome = $"{Prefix} Cliente Base", Telefone = "910000000" };
        var reparacao = new Reparacao
        {
            TenantId = tenantId,
            Numero = 3201,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Equipamento = $"{Prefix} iPhone Base",
            Avaria = "Teste",
            EntregueEm = date,
            PrecoFinalCents = 10_000,
            EstadoPagamento = PaymentStatus.Pago,
        };
        var part = new Part { TenantId = tenantId, Nome = $"{Prefix} Ecra Base", QtdStock = 5, CustoUnitarioCents = 1_000, Fornecedor = "Fornecedor Base" };
        var movimento = new PartMovimento
        {
            TenantId = tenantId,
            Part = part,
            PartId = part.Id,
            Quantidade = -2,
            StockAntes = 5,
            StockDepois = 3,
            Motivo = PartMovimentoMotivo.UsoEmReparacao,
            Reparacao = reparacao,
            ReparacaoId = reparacao.Id,
            Notas = Prefix,
        };
        db.AddRange(cliente, reparacao, part, movimento,
            new Trabalho
            {
                TenantId = tenantId,
                Numero = 3202,
                Cliente = cliente,
                ClienteId = cliente.Id,
                Titulo = $"{Prefix} Trabalho Base",
                Status = TrabalhoStatus.Concluido,
                DataConclusao = date,
                PrecoFinalCents = 5_000,
                EstadoPagamento = PaymentStatus.Pago,
            },
            new Venda
            {
                TenantId = tenantId,
                Numero = 3203,
                Data = date,
                TotalCents = 7_000,
                Status = VendaStatus.Paga,
            },
            new Despesa
            {
                TenantId = tenantId,
                Descricao = $"{Prefix} OpEx Base",
                Categoria = DespesaCategoria.Servicos,
                ValorCents = 3_000,
                Data = date,
                IsCogs = false,
            });
        await db.SaveChangesAsync();
        movimento.CreatedAt = date;
        await db.SaveChangesAsync();
    }

    private async Task SeedTopScenarioAsync(Guid tenantId, int ano)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var date = new DateTime(ano, 2, 12, 12, 0, 0, DateTimeKind.Utc);
        var cliente = new Cliente { TenantId = tenantId, Nome = $"{Prefix} Cliente Tops", Telefone = "910000000" };
        var reparacaoA = new Reparacao
        {
            TenantId = tenantId,
            Numero = 3501,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Equipamento = $"{Prefix} Pro",
            Avaria = "Teste",
            EntregueEm = date,
            PrecoFinalCents = 12_000,
            EstadoPagamento = PaymentStatus.Pago,
        };
        var reparacaoB = new Reparacao
        {
            TenantId = tenantId,
            Numero = 3502,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Equipamento = $"{Prefix} Mini",
            Avaria = "Teste",
            EntregueEm = date,
            PrecoFinalCents = 3_000,
            EstadoPagamento = PaymentStatus.Pago,
        };
        var ecra = new Part { TenantId = tenantId, Nome = $"{Prefix} Ecra", Sku = "NEG-ECRA", QtdStock = 10, CustoUnitarioCents = 1_000, Fornecedor = "Fornecedor A" };
        var bateria = new Part { TenantId = tenantId, Nome = $"{Prefix} Bateria", Sku = "NEG-BAT", QtdStock = 10, CustoUnitarioCents = 500, Fornecedor = "Fornecedor B" };
        var movimentos = new[]
        {
            new PartMovimento { TenantId = tenantId, Part = ecra, PartId = ecra.Id, Quantidade = -3, StockAntes = 10, StockDepois = 7, Motivo = PartMovimentoMotivo.UsoEmReparacao, Reparacao = reparacaoA, ReparacaoId = reparacaoA.Id, Notas = Prefix },
            new PartMovimento { TenantId = tenantId, Part = bateria, PartId = bateria.Id, Quantidade = -1, StockAntes = 10, StockDepois = 9, Motivo = PartMovimentoMotivo.UsoEmReparacao, Reparacao = reparacaoB, ReparacaoId = reparacaoB.Id, Notas = Prefix },
            new PartMovimento { TenantId = tenantId, Part = ecra, PartId = ecra.Id, Quantidade = 5, StockAntes = 5, StockDepois = 10, Motivo = PartMovimentoMotivo.Entrada, Notas = Prefix },
            new PartMovimento { TenantId = tenantId, Part = bateria, PartId = bateria.Id, Quantidade = 6, StockAntes = 4, StockDepois = 10, Motivo = PartMovimentoMotivo.Entrada, Notas = Prefix },
        };
        db.AddRange(cliente, reparacaoA, reparacaoB, ecra, bateria);
        db.PartMovimentos.AddRange(movimentos);
        await db.SaveChangesAsync();
        foreach (var movimento in movimentos) movimento.CreatedAt = date;
        await db.SaveChangesAsync();
    }

    private async Task SeedOtherTenantNoiseAsync(Guid tenantId, int ano)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var date = new DateTime(ano, 2, 12, 12, 0, 0, DateTimeKind.Utc);
        var cliente = new Cliente { TenantId = tenantId, Nome = $"{Prefix} Outro Tenant", Telefone = "910000000" };
        db.AddRange(cliente, new Reparacao
        {
            TenantId = tenantId,
            Numero = 3599,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Equipamento = $"{Prefix} Outro Tenant",
            Avaria = "Teste",
            EntregueEm = date,
            PrecoFinalCents = 99_999,
            EstadoPagamento = PaymentStatus.Pago,
        });
        await db.SaveChangesAsync();
    }

    private async Task CleanupPeriodAsync(Guid tenantId, int ano, int trimestre)
    {
        var from = new DateTime(ano, (trimestre - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(3);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var movimentos = await db.PartMovimentos.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && (m.CreatedAt >= from && m.CreatedAt < to || m.Notas == Prefix))
            .ToListAsync();
        db.PartMovimentos.RemoveRange(movimentos);
        var vendaItems = await db.VendaItems.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.Venda != null && i.Venda.Data >= from && i.Venda.Data < to)
            .ToListAsync();
        db.VendaItems.RemoveRange(vendaItems);
        await db.SaveChangesAsync();

        db.Despesas.RemoveRange(db.Despesas.IgnoreQueryFilters().Where(d => d.TenantId == tenantId && d.Data >= from && d.Data < to));
        db.Reparacoes.RemoveRange(db.Reparacoes.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && r.EntregueEm >= from && r.EntregueEm < to));
        db.Trabalhos.RemoveRange(db.Trabalhos.IgnoreQueryFilters().Where(t => t.TenantId == tenantId && t.DataConclusao >= from && t.DataConclusao < to));
        db.Vendas.RemoveRange(db.Vendas.IgnoreQueryFilters().Where(v => v.TenantId == tenantId && v.Data >= from && v.Data < to));
        db.Parts.RemoveRange(db.Parts.IgnoreQueryFilters().Where(p => p.TenantId == tenantId && p.Nome.StartsWith(Prefix)));
        db.Clientes.RemoveRange(db.Clientes.IgnoreQueryFilters().Where(c => c.TenantId == tenantId && c.Nome.StartsWith(Prefix)));
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
