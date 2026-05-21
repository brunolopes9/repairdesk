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

public class RelatoriosIvaApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public RelatoriosIvaApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RegimeNormal_T2_CalculaIva23PorCento()
    {
        await SeedTenantAsync(RepairDeskApiFactory.TenantId, RegimeFiscal.RegimeNormalIva);
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioIvaResponse>("/api/relatorios/iva?ano=2026&trimestre=2");

        // Sprint 159b: PrecoFinalCents é COM IVA. Extracção IVA embutido (base = total × 100/123):
        // Reparação 10000 → base 8130, IVA 1870
        // Trabalho   20000 → base 16260, IVA 3740
        // Soma: base 24390, IVA 5610.
        report!.TotalSemIvaCents.Should().Be(24390);
        report.IvaLiquidadoCents.Should().Be(5610);
        report.IvaAEntregarCents.Should().Be(5610);
        report.Documentos.Should().HaveCount(2);
        // base + IVA = total (invariant matemático mais robusto que "IVA = base × 23%" porque
        // arredondamento pode introduzir delta de 1 cent).
        report.Documentos.Should().OnlyContain(d => d.BaseCents + d.IvaCents == d.TotalCents);
    }

    [Fact]
    public async Task IsencaoArt53_DevolveIvaZero()
    {
        await SeedTenantAsync(RepairDeskApiFactory.SecondTenantId, RegimeFiscal.IsentoArt53);
        var client = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);

        var report = await client.GetFromJsonAsync<RelatorioIvaResponse>("/api/relatorios/iva?ano=2026&trimestre=2");

        report!.TotalSemIvaCents.Should().Be(30000);
        report.IvaLiquidadoCents.Should().Be(0);
        report.IvaAEntregarCents.Should().Be(0);
        report.Documentos.Should().OnlyContain(d => d.IvaCents == 0 && d.TotalCents == d.BaseCents);
    }

    [Fact]
    public async Task ExportCsv_UsaUtf8BomEColunasExcelFriendly()
    {
        await SeedTenantAsync(RepairDeskApiFactory.TenantId, RegimeFiscal.RegimeNormalIva);
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var bytes = await client.GetByteArrayAsync("/api/relatorios/iva/export.csv?ano=2026&trimestre=2");

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        text.Should().Contain("data,tipo,numero,cliente,base,iva,total");
        text.Should().Contain("FA 2026/1001");
    }

    private async Task SeedTenantAsync(Guid tenantId, RegimeFiscal regime)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        tenant.RegimeFiscal = regime;

        db.Reparacoes.RemoveRange(db.Reparacoes.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && r.InvoiceNumber != null));
        db.Trabalhos.RemoveRange(db.Trabalhos.IgnoreQueryFilters().Where(t => t.TenantId == tenantId && t.InvoiceNumber != null));
        db.Clientes.RemoveRange(db.Clientes.IgnoreQueryFilters().Where(c => c.TenantId == tenantId && c.Nome.StartsWith("Fiscal Test")));
        await db.SaveChangesAsync();

        var cliente = new Cliente { TenantId = tenantId, Nome = "Fiscal Test Cliente", Telefone = "910000000", Nif = "123456789" };
        db.Clientes.Add(cliente);
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = tenantId,
            Numero = 1001,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Equipamento = "iPhone",
            Avaria = "Ecra partido",
            PrecoFinalCents = 10000,
            EstadoPagamento = PaymentStatus.Pago,
            InvoiceProvider = BillingProvider.Moloni,
            InvoiceExternalId = $"rep-{tenantId:N}",
            InvoiceNumber = "FA 2026/1001",
            InvoiceEmittedAt = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
        });
        db.Trabalhos.Add(new Trabalho
        {
            TenantId = tenantId,
            Numero = 2001,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Titulo = "Site",
            Categoria = JobCategory.Website,
            PrecoFinalCents = 20000,
            EstadoPagamento = PaymentStatus.Pago,
            Status = TrabalhoStatus.Concluido,
            InvoiceProvider = BillingProvider.Moloni,
            InvoiceExternalId = $"trab-{tenantId:N}",
            InvoiceNumber = "FA 2026/2001",
            InvoiceEmittedAt = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
        });
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
