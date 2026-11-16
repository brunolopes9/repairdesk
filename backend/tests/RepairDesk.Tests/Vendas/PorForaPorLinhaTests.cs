using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Vendas;

/// <summary>Sprint 680: «faturar a» por linha — o faturado leva IVA, a diferença vai por fora e entra inteira no lucro.</summary>
public class PorForaPorLinhaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PorForaPorLinhaTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task FaturarAbaixoDoPreco_DiferencaVaiPorFora()
    {
        var c = await Admin();
        // Cabo cobrado a 8,90 €, faturado a 6,00 € (2,90 € por fora); serviço sem custo para as contas serem simples.
        var v = (await (await c.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Cabo USB-C", 1, 600, IvaRate: 23m, PorForaCents: 290)], VendaEstado.Orcamento)))
            .Content.ReadFromJsonAsync<VendaDto>())!;

        v.TotalCents.Should().Be(890, "o cliente paga o preço todo");
        var item = v.Items.Single();
        item.TotalCents.Should().Be(600, "a fatura só leva o faturado");
        item.PorForaCents.Should().Be(290);
        v.IvaCents.Should().Be(112, "IVA só dos 6,00 € (6 − 6/1,23 = 1,12)");
        item.Lucro.Should().BeApproximately(6.00m - 6.00m / 1.23m * 0.23m + 2.90m, 0.005m);

        // Linha toda por fora ignora o «por fora» da linha.
        var v2 = (await (await c.PutAsJsonAsync($"/api/vendas/{v.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(item.Id, null, "Cabo USB-C", 1, 890, PorFora: true, PorForaCents: 290)])))
            .Content.ReadFromJsonAsync<VendaDto>())!;
        v2.TotalCents.Should().Be(890);
        v2.Items.Single().PorForaCents.Should().Be(0);
    }

    private async Task<HttpClient> Admin()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
