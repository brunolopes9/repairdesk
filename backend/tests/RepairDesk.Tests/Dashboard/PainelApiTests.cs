using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Dashboard;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;
using RepairDesk.Tests.Support;

namespace RepairDesk.Tests.Dashboard;

/// <summary>Dashboard do modelo novo (Doc 94): números do mês vêm do motor de IVA; em curso e alertas refletem as vendas.</summary>
public class PainelApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PainelApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Painel_ReflecteVendaEntregue_EmCurso_EFaturaPorRegistar()
    {
        var client = await AuthedClient(RepairDeskApiFactory.AdminEmail);
        var antes = (await client.GetFromJsonAsync<PainelDto>("/api/dashboard"))!;

        // Serviço de 369 € entregue sem fatura registada: IVA 69 €, lucro 300 €.
        (await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, "Website", null,
            [new VendaLinhaWriteRequest(null, null, "Website", 1, 36900)], VendaEstado.Entregue))).EnsureSuccessStatusCode();
        // Reparação pronta para levantar.
        await TestReparacoes.CriarAsync(client, "Samsung A52", estado: VendaEstado.Pronta);

        var depois = (await client.GetFromJsonAsync<PainelDto>("/api/dashboard"))!;
        (depois.Mes.Faturado - antes.Mes.Faturado).Should().Be(369m);
        Math.Round(depois.Mes.IvaAEntregar - antes.Mes.IvaAEntregar, 2).Should().Be(69m);
        Math.Round(depois.Mes.LucroVendas - antes.Mes.LucroVendas, 2).Should().Be(300m);
        (depois.Alertas.FaturasPorRegistar - antes.Alertas.FaturasPorRegistar).Should().Be(1);
        (depois.EmCurso.Prontas - antes.EmCurso.Prontas).Should().Be(1);
        depois.ProximasEntregas.Should().Contain(v => v.Descricao == "Samsung A52" && v.Estado == VendaEstado.Pronta);
    }

    [Fact]
    public async Task Painel_IsolaTenants()
    {
        var a = await AuthedClient(RepairDeskApiFactory.AdminEmail);
        var b = await AuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var antesA = (await a.GetFromJsonAsync<PainelDto>("/api/dashboard"))!;

        (await b.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, "Iso B", null,
            [new VendaLinhaWriteRequest(null, null, "Software", 1, 40000)], VendaEstado.Entregue))).EnsureSuccessStatusCode();

        var depoisA = (await a.GetFromJsonAsync<PainelDto>("/api/dashboard"))!;
        depoisA.Mes.Faturado.Should().Be(antesA.Mes.Faturado);
    }

    private async Task<HttpClient> AuthedClient(string email)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
