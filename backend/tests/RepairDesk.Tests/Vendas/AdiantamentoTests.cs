using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Vendas;

/// <summary>Sprint 679: adiantamento pedido no orçamento — valor, recebido (auditado), sai no PDF.</summary>
public class AdiantamentoTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AdiantamentoTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Adiantamento_PedidoRecebidoEMantidoNasGravacoes()
    {
        var c = await Admin();
        var linhas = new List<VendaLinhaWriteRequest> { new(null, null, "Troca de carcaça iPhone", 1, 23000) };
        var venda = (await (await c.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            linhas, VendaEstado.Orcamento))).Content.ReadFromJsonAsync<VendaDto>())!;
        venda.AdiantamentoCents.Should().BeNull();

        // Sem adiantamento pedido não se pode marcar como recebido.
        (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/adiantamento", new AdiantamentoRecebidoRequest(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var linhasIds = venda.Items.Select(i => new VendaLinhaWriteRequest(i.Id, null, i.Descricao, 1, i.PrecoUnitarioCents)).ToList();
        var com = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            linhasIds, AdiantamentoCents: 5750))).Content.ReadFromJsonAsync<VendaDto>())!;
        com.AdiantamentoCents.Should().Be(5750);

        // Gravar sem o campo (null) não apaga o adiantamento.
        var semCampo = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, "nota",
            linhasIds))).Content.ReadFromJsonAsync<VendaDto>())!;
        semCampo.AdiantamentoCents.Should().Be(5750);

        var pago = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/adiantamento", new AdiantamentoRecebidoRequest(true, "FR 2026/12"))).Content.ReadFromJsonAsync<VendaDto>())!;
        pago.AdiantamentoRecebidoEm.Should().NotBeNull();
        pago.AdiantamentoFatura.Should().Be("FR 2026/12");

        // Já recebido: não se tira o adiantamento sem desfazer primeiro.
        (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            linhasIds, AdiantamentoCents: 0))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var pdf = await c.GetAsync($"/api/vendas/{venda.Id}/orcamento.pdf");
        pdf.EnsureSuccessStatusCode();
        (await pdf.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);

        var desfeito = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/adiantamento", new AdiantamentoRecebidoRequest(false))).Content.ReadFromJsonAsync<VendaDto>())!;
        desfeito.AdiantamentoRecebidoEm.Should().BeNull();
        var tirado = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            linhasIds, AdiantamentoCents: 0))).Content.ReadFromJsonAsync<VendaDto>())!;
        tirado.AdiantamentoCents.Should().BeNull();
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
