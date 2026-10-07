using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.PublicPortal;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;
using RepairDesk.Tests.Support;

namespace RepairDesk.Tests.Reparacoes;

/// <summary>Doc 94 Fase 4c: a reparação é uma Venda — portal do cliente, aceitação do orçamento, garantia e PDFs.</summary>
public class PortalReparacaoApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PortalReparacaoApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ClienteAceitaOrcamentoNoPortal_PassaAEmCurso_ComLinhaTemporal()
    {
        var admin = await AuthedClient();
        var rep = await TestReparacoes.CriarAsync(admin, "Xiaomi Redmi Note 12", "Não carrega", 4500);
        rep.PublicSlug.Should().NotBeNullOrWhiteSpace();

        var anon = _factory.CreateClient();
        var portal = (await anon.GetFromJsonAsync<PublicRepairDto>($"/api/public/repair/{rep.PublicSlug}"))!;
        portal.Estado.Should().Be(PublicEstado.Orcamento);
        portal.OrcamentoCents.Should().Be(4500);
        portal.EquipamentoPublico.Should().Be("Xiaomi Redmi Note 12");

        var aceitar = await anon.PostAsJsonAsync($"/api/public/repair/{rep.PublicSlug}/orcamento", new AprovarOrcamentoRequest(true));
        aceitar.StatusCode.Should().Be(HttpStatusCode.OK, await aceitar.Content.ReadAsStringAsync());
        var depois = (await aceitar.Content.ReadFromJsonAsync<PublicRepairDto>())!;
        depois.Estado.Should().Be(PublicEstado.EmReparacao);
        depois.OrcamentoAprovado.Should().BeTrue();
        depois.Timeline.Select(t => t.Estado).Should().ContainInOrder(PublicEstado.Orcamento, PublicEstado.EmReparacao);

        var venda = (await admin.GetFromJsonAsync<VendaDto>($"/api/vendas/{rep.Id}"))!;
        venda.Estado.Should().Be(VendaEstado.EmCurso);
    }

    [Fact]
    public async Task EntregarReparacao_EmiteGarantia_EPdfsDeEntradaEEntrega()
    {
        var admin = await AuthedClient();
        var rep = await TestReparacoes.CriarAsync(admin, estado: VendaEstado.Pronta);

        var entregue = await admin.PostAsJsonAsync($"/api/vendas/{rep.Id}/estado", new MudarEstadoVendaRequest(VendaEstado.Entregue, PaymentMethod.MBWay));
        entregue.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await entregue.Content.ReadFromJsonAsync<VendaDto>())!;
        dto.GarantiaSlug.Should().NotBeNullOrWhiteSpace();

        var garantia = await _factory.CreateClient().GetFromJsonAsync<PublicGarantiaDto>($"/api/public/warranty/{dto.GarantiaSlug}");
        garantia!.Origem.Should().Be("Reparacao");
        garantia.EquipamentoPublico.Should().Be("iPhone 13");

        foreach (var pdf in new[] { "entrada.pdf", "entrega.pdf", "label.pdf", "orcamento.pdf" })
        {
            var resp = await admin.GetAsync($"/api/vendas/{rep.Id}/{pdf}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK, pdf);
            System.Text.Encoding.ASCII.GetString(await resp.Content.ReadAsByteArrayAsync(), 0, 4).Should().Be("%PDF");
        }
    }

    [Fact]
    public async Task VendaDeProduto_NaoTemPortal()
    {
        var admin = await AuthedClient();
        var resp = await admin.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Capa", 1, 900)]));
        var venda = (await resp.Content.ReadFromJsonAsync<VendaDto>())!;
        venda.PublicSlug.Should().BeNull();
    }

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
