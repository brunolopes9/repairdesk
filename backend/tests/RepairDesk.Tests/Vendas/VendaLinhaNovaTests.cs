using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Vendas;

/// <summary>Sprint 652: acrescentar a 1.ª linha a uma venda já gravada dava 500 (DbUpdateConcurrencyException) — a linha nova traz Id e o EF tratava-a como existente.</summary>
public class VendaLinhaNovaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public VendaLinhaNovaTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Reparacao_OrcamentoSemLinhas_GuardarComServicoECustoSemFatura()
    {
        var c = _factory.CreateClient();
        var login = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var cliente = (await (await c.PostAsJsonAsync("/api/clientes", new CreateClienteRequest("Andreia", "912000111", null, null, null)))
            .Content.ReadFromJsonAsync<ClienteDto>())!;
        var criar = await c.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Reparacao, cliente.Id, "Samsung A52", "A diagnosticar", null, []));
        var corpo = await criar.Content.ReadAsStringAsync();
        criar.StatusCode.Should().Be(HttpStatusCode.Created, corpo);
        var venda = (await criar.Content.ReadFromJsonAsync<VendaDto>())!;

        var put = await c.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Reparacao, cliente.Id, "Samsung A52", "A diagnosticar",
            "Do Excel de novembro (02/11). Por faturar.",
            [new VendaLinhaWriteRequest(null, null, "LCD + película gel Samsung A52", 1, 12300, IvaRate: 23m, PorFora: false, CustoSemFaturaCents: 6000)]));
        var txt = await put.Content.ReadAsStringAsync();
        put.StatusCode.Should().Be(HttpStatusCode.OK, txt);
    }
}
