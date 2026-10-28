using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Artigos;

/// <summary>
/// Sprint 581: SKU do fornecedor reconhece as compras seguintes; artigos agrupados (um código, vários modelos) com
/// escolha do modelo no balcão; juntar artigos sem perder as etiquetas antigas; peças de reparação fora do "por ligar".
/// </summary>
public class ArtigosAgrupadosTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ArtigosAgrupadosTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CompraSeguinte_ComOMesmoSkuDoFornecedor_LigaSeSozinhaAoArtigo()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client);
        var sku = "RIX" + Random.Shared.Next(10000, 99999);
        var primeira = await Compra(client, forn, new CompraLinhaWriteRequest(null, "Rixus Clear HD Tempered Glass With Fit Box For Apple iPhone 16", 1, 2.45m, null, null, null, sku));
        var artigo = await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película premium " + sku, "Películas", 2000, null, null, null, null, true, [primeira.Linhas.Single().Id], Agrupado: true));

        // Outra fatura, descrição ligeiramente diferente, mesmo SKU → mesmo artigo, sem fazer nada.
        var segunda = await Compra(client, forn, new CompraLinhaWriteRequest(null, "Rixus Clear HD Tempered Glass With Fit Box For iPhone 16 (new)", 3, 2.40m, null, null, null, sku.ToLowerInvariant()));
        segunda.Linhas.Single().ArtigoId.Should().Be(artigo.Artigo.Id);
        segunda.Linhas.Single().SkuFornecedor.Should().Be(sku);
        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{artigo.Artigo.Id}"))!.Artigo.Stock.Should().Be(4);
    }

    [Fact]
    public async Task SemSku_AMesmaDescricao_FicaComoSugestao_NaoLigaSozinha()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client);
        var desc = "VIDRO TEMPERADO NORMAL " + Guid.NewGuid().ToString("N")[..6];
        var primeira = await Compra(client, forn, new CompraLinhaWriteRequest(null, desc, 5, 0.74m, null, null, null));
        var artigo = await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película vidro " + desc, "Películas", null, null, null, null, null, true, [primeira.Linhas.Single().Id]));

        var segunda = await Compra(client, forn, new CompraLinhaWriteRequest(null, "  " + desc.ToLowerInvariant() + " ", 5, 0.74m, null, null, null));
        segunda.Linhas.Single().ArtigoId.Should().BeNull("uma descrição pode enganar — só o SKU confirmado liga sozinho");
        var sugestao = (await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes"))!
            .Single(x => x.LoteIds.Contains(segunda.Linhas.Single().Id));
        sugestao.ArtigoSugeridoId.Should().Be(artigo.Artigo.Id);
    }

    [Fact]
    public async Task Agrupado_BalcaoVendeDoModeloEscolhido_EFaturaDizOModelo()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client);
        var compra = await Compra(client, forn,
            new CompraLinhaWriteRequest(null, "Rixus curva iPhone XR / 11", 2, 1.75m, null, null, null),
            new CompraLinhaWriteRequest(null, "Rixus curva iPhone 17", 1, 1.75m, null, null, null));
        var xr = compra.Linhas.Single(l => l.Descricao.Contains("XR")).Id;
        var a = (await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película curva " + Guid.NewGuid().ToString("N")[..4], "Películas", 1500, null, null, null, null, true,
                compra.Linhas.Select(l => l.Id).ToList(), Agrupado: true))).Artigo;
        a.Agrupado.Should().BeTrue();

        // O lote do iPhone 17 é o mais antigo pela ordem, mas o cliente quer o do iPhone 11.
        var venda = await Post<VendaDto>(client, "/api/artigos/balcao/vender",
            new BalcaoVendaRequest([new BalcaoLinhaRequest(a.Id, 1, null, xr)], PaymentMethod.Dinheiro));
        venda.Items.Single().CompraLinhaId.Should().Be(xr);
        venda.Items.Single().Descricao.Should().Contain("iPhone XR / 11");
    }

    [Fact]
    public async Task Fundir_PassaLotes_EAsEtiquetasAntigasContinuamALer()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client);
        var compra = await Compra(client, forn,
            new CompraLinhaWriteRequest(null, "Vidro temperado Hoco Samsung A12", 1, 1.23m, null, null, null),
            new CompraLinhaWriteRequest(null, "Vidro temperado Hoco Samsung A16 5G", 3, 1.23m, null, null, null));
        var antigo = (await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Vidro A12 antigo", "Películas", null, null, null, null, null, true, [compra.Linhas[0].Id]))).Artigo;
        var novo = (await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película vidro " + Guid.NewGuid().ToString("N")[..4], "Películas", 1500, null, null, null, null, true, [compra.Linhas[1].Id], Agrupado: true))).Artigo;

        var junto = await Post<ArtigoDetalheDto>(client, $"/api/artigos/{novo.Id}/fundir", new FundirArtigosRequest([antigo.Id]));
        junto.Artigo.Stock.Should().Be(4);
        junto.Codigos!.Should().Contain(c => c.Codigo == antigo.CodigoBarras && c.FornecedorId == null);

        // A etiqueta antiga (já colada na película) leva ao artigo novo.
        (await client.GetFromJsonAsync<ArtigoDto>($"/api/artigos/codigo/{antigo.CodigoBarras}"))!.Id.Should().Be(novo.Id);
        (await client.GetFromJsonAsync<List<ArtigoDto>>("/api/artigos"))!.Should().NotContain(x => x.Id == antigo.Id);

        // Desfazer: o artigo antigo volta, com o seu lote e a sua etiqueta.
        var fusao = junto.Fusoes!.Single(f => f.OrigemSku == antigo.Sku);
        var reposto = await Post<ArtigoDetalheDto>(client, $"/api/artigos/fusoes/{fusao.Id}/desfazer", new { });
        reposto.Artigo.Id.Should().Be(antigo.Id);
        reposto.Artigo.Stock.Should().Be(1);
        (await client.GetFromJsonAsync<ArtigoDto>($"/api/artigos/codigo/{antigo.CodigoBarras}"))!.Id.Should().Be(antigo.Id);
        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{novo.Id}"))!.Artigo.Stock.Should().Be(3);
    }

    [Fact]
    public async Task JuntarFornecedores_LevaOsSkusAprendidos()
    {
        var client = await AuthedClient();
        var origem = await CriarFornecedor(client);
        var destino = await CriarFornecedor(client);
        var sku = "SKU" + Random.Shared.Next(10000, 99999);
        var compra = await Compra(client, origem, new CompraLinhaWriteRequest(null, "Soft OLED iPhone 13", 1, 29.95m, null, null, null, sku));
        var artigo = await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Ecrã Soft-OLED iPhone 13 " + sku, null, null, null, null, null, null, true, [compra.Linhas.Single().Id]));

        (await client.PostAsJsonAsync($"/api/fornecedores/{destino}/fundir", new RepairDesk.API.Controllers.FundirFornecedorRequest(origem))).EnsureSuccessStatusCode();

        // A próxima compra do mesmo SKU, já no fornecedor que ficou, liga sozinha.
        var nova = await Compra(client, destino, new CompraLinhaWriteRequest(null, "Soft OLED iPhone 13", 1, 29.95m, null, null, null, sku));
        nova.Linhas.Single().ArtigoId.Should().Be(artigo.Artigo.Id);
    }

    [Fact]
    public async Task PecaDeReparacao_SaiDosLotesPorLigar()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client);
        var desc = "Touch e lcd para iph 11 preto " + Guid.NewGuid().ToString("N")[..6];
        var compra = await Compra(client, forn, new CompraLinhaWriteRequest(null, desc, 1, 14.90m, null, null, null));
        (await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes"))!.Should().Contain(s => s.Nome == desc);

        (await client.PutAsJsonAsync("/api/artigos/lotes/peca-reparacao", new PecaReparacaoRequest([compra.Linhas.Single().Id]))).EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes"))!.Should().NotContain(s => s.Nome == desc);
    }

    // ---------- helpers ----------

    private static async Task<Guid> CriarFornecedor(HttpClient client)
    {
        var forn = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.UeAutoliquidacao, "NL"));
        forn.EnsureSuccessStatusCode();
        return (await forn.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
    }

    private static Task<CompraDocumentoDto> Compra(HttpClient client, Guid fornecedorId, params CompraLinhaWriteRequest[] linhas)
        => Post<CompraDocumentoDto>(client, "/api/compras", new CompraDocumentoWriteRequest(fornecedorId, new DateTime(2026, 10, 26),
            "I2026-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null, linhas));

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var resp = await client.PostAsJsonAsync(url, body);
        if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<T>())!;
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
