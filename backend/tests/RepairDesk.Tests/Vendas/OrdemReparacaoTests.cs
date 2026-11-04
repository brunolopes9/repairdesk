using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;
using UglyToad.PdfPig;

namespace RepairDesk.Tests.Vendas;

/// <summary>
/// Sprint 595: ordem de reparação digital — a ficha (IMEI, senha, testes, checklist), o cliente assina pelo link,
/// o PDF tem 2 vias (só a da loja mostra a senha) e a senha apaga-se ao entregar.
/// </summary>
[Collection("Pdf")]
public class OrdemReparacaoTests : IClassFixture<RepairDeskApiFactory>
{
    // PNG 1×1 transparente.
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private readonly RepairDeskApiFactory _factory;
    public OrdemReparacaoTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ficha_ClienteAssinaPeloLink_PdfComDuasVias_SenhaSoNaViaDaLoja_EApagaAoEntregar()
    {
        var client = await AuthedClient();
        var cliente = (await (await client.PostAsJsonAsync("/api/clientes", new CreateClienteRequest("Andreia Teste", "912000111", null, null, null)))
            .Content.ReadFromJsonAsync<ClienteDto>())!.Id;
        var venda = (await (await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Reparacao, cliente, "iPad 9", "Ecrã partido", null,
            [new VendaLinhaWriteRequest(null, null, "Substituição do ecrã", 1, 6000)], VendaEstado.EmCurso, null)))
            .Content.ReadFromJsonAsync<VendaDto>())!;

        var guardar = await client.PutAsJsonAsync($"/api/vendas/{venda.Id}/ficha", new FichaReparacaoWriteRequest(
            "356938035643809", SenhaTipo.Pin, "482915", true, "Riscos na traseira", "Capa",
            [new ChecklistItemDto("ecra", "", "falha", null), new ChecklistItemDto("wifi", "", "ok", null)]));
        guardar.StatusCode.Should().Be(HttpStatusCode.OK, await guardar.Content.ReadAsStringAsync());
        var ficha = (await guardar.Content.ReadFromJsonAsync<FichaReparacaoDto>())!;
        ficha.Senha.Should().Be("482915");
        ficha.Checklist.Single(c => c.Chave == "ecra").Entrada.Should().Be("falha");
        ficha.ClienteAssinouEm.Should().BeNull();

        // PIN inválido e padrão inválido são recusados.
        (await client.PutAsJsonAsync($"/api/vendas/{venda.Id}/ficha", new FichaReparacaoWriteRequest(null, SenhaTipo.Pin, "12", null, null, null, null)))
            .StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/vendas/{venda.Id}/ficha", new FichaReparacaoWriteRequest(null, SenhaTipo.Padrao, "1-1-2-3", null, null, null, null)))
            .StatusCode.Should().NotBe(HttpStatusCode.OK);

        // O link público mostra a ordem, sem a senha, e deixa assinar uma vez.
        var publico = _factory.CreateClient();
        var json = await publico.GetStringAsync($"/api/public/repair/{venda.PublicSlug}/ficha");
        json.Should().Contain("Substituição do ecrã").And.NotContain("482915");
        var assinar = await publico.PostAsJsonAsync($"/api/public/repair/{venda.PublicSlug}/assinar", new AssinarFichaRequest(Png, true));
        assinar.StatusCode.Should().Be(HttpStatusCode.OK, await assinar.Content.ReadAsStringAsync());
        (await assinar.Content.ReadFromJsonAsync<PublicFichaDto>())!.AssinadaEm.Should().NotBeNull();
        (await publico.PostAsJsonAsync($"/api/public/repair/{venda.PublicSlug}/assinar", new AssinarFichaRequest(Png, true)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var aposAssinar = (await client.GetFromJsonAsync<FichaReparacaoDto>($"/api/vendas/{venda.Id}/ficha"))!;
        aposAssinar.ClienteAssinouEm.Should().NotBeNull();
        aposAssinar.ClienteAssinouPeloLink.Should().BeTrue();

        // PDF da loja: 2 folhas (ordem + checklist), a senha só na via da loja; o do cliente nunca tem a senha.
        using (var pdf = PdfDocument.Open(await client.GetByteArrayAsync($"/api/vendas/{venda.Id}/ordem.pdf")))
        {
            pdf.NumberOfPages.Should().Be(2);
            var texto = pdf.GetPage(1).Text;
            texto.Should().Contain("VIA DA LOJA").And.Contain("VIA DO CLIENTE").And.Contain("482915").And.Contain("356938035643809");
            pdf.GetPage(2).Text.Should().Contain("CHECKLIST");
        }
        using (var pdf = PdfDocument.Open(await publico.GetByteArrayAsync($"/api/public/repair/{venda.PublicSlug}/ordem.pdf")))
            pdf.GetPage(1).Text.Should().Contain("VIA DO CLIENTE").And.NotContain("VIA DA LOJA").And.NotContain("482915");

        // Entregue: a senha deixa de estar guardada.
        (await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/estado", new MudarEstadoVendaRequest(VendaEstado.Entregue, PaymentMethod.Dinheiro)))
            .EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<FichaReparacaoDto>($"/api/vendas/{venda.Id}/ficha"))!.Senha.Should().BeNull();
    }

    [Theory]
    [InlineData("1-5-9-6", true)]
    [InlineData("1-2-3-4-5-6-7-8-9", true)]
    [InlineData("1-2-3", false)]
    [InlineData("1-2-2-3", false)]
    [InlineData("0-1-2-3", false)]
    public void Padrao_Validacao(string padrao, bool valido) => FichaReparacaoService.PadraoValido(padrao).Should().Be(valido);

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        return client;
    }
}
