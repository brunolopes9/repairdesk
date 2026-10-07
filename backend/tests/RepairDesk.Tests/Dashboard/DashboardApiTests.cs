using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Dashboard;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Dashboard;

public class DashboardApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DashboardApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_ReflectsVendaEntregueAndDespesa()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        // Cria cliente para o trabalho (ClienteId agora obrigatório)
        var phone = "9" + Random.Shared.Next(10000000, 99999999).ToString();
        var clienteResp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Junta de Freguesia", phone, null, null, null));
        clienteResp.EnsureSuccessStatusCode();
        var cliente = (await clienteResp.Content.ReadFromJsonAsync<ClienteDto>())!;

        // Serviço (website) entregue e pago
        var create = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Servico, cliente.Id, null, "Site Junta", null,
            [new VendaLinhaWriteRequest(null, null, "Website", 1, 60000)], VendaEstado.Entregue, PaymentMethod.TransferenciaBancaria));
        create.EnsureSuccessStatusCode();

        // Despesa do mês
        var dResp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Domínio + hosting", DespesaCategoria.Software, 7500, DateTime.UtcNow, "Cloudflare", null, null, null));
        dResp.EnsureSuccessStatusCode();

        var dash = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");
        dash!.Kpis.ReceitaCentsMes.Should().BeGreaterThanOrEqualTo(60000);
        dash.Kpis.DespesasCentsMes.Should().BeGreaterThanOrEqualTo(7500);
        dash.Kpis.LucroCentsMes.Should().Be(dash.Kpis.ReceitaCentsMes - dash.Kpis.DespesasCentsMes);
        dash.ReceitaPorCategoria.Should().Contain(c => c.Label == "Vendas");
        dash.DespesaPorCategoria.Should().Contain(c => c.Label == "Software");
    }

    [Fact]
    public async Task Dashboard_TenantA_DoesNotSeeTenantB_Numbers()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);

        // B vende 400 € (serviço entregue)
        (await clientB.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, "Iso B", null,
            [new VendaLinhaWriteRequest(null, null, "Software", 1, 40000)], VendaEstado.Entregue))).EnsureSuccessStatusCode();
        var antesA = await clientA.GetFromJsonAsync<DashboardResponse>("/api/dashboard");

        var dashA = await clientA.GetFromJsonAsync<DashboardResponse>("/api/dashboard");
        // A não vê a venda de B
        dashA!.Kpis.ReceitaCentsMes.Should().Be(antesA!.Kpis.ReceitaCentsMes);
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
