using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Artigos;

/// <summary>Sprint 566: artigos da loja (SKU + EAN-13), etiquetas e venda ao balcão pela pistola.</summary>
public class ArtigosApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ArtigosApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("123", false)]
    public void Ean13_Valida(string codigo, bool valido) => Ean13.Valido(codigo).Should().Be(valido);

    [Fact]
    public void Ean13_InternoTemPrefixo20_EDesenha95Modulos()
    {
        var c = Ean13.Interno(12);
        c.Should().HaveLength(13).And.StartWith("200000000012");
        Ean13.Valido(c).Should().BeTrue();
        Ean13.Modulos(c).Should().HaveLength(95);
        Ean13.Svg(c).Should().StartWith("<svg").And.Contain("<rect");
    }

    [Fact]
    public async Task Artigo_JuntaLotes_PistolaEncontra_BalcaoVendeDoLoteMaisAntigo()
    {
        var client = await AuthedClient();
        var lote1 = await CriarLote(client, "Película Samsung A34 (compra 1)", 2, 1.50m);
        var lote2 = await CriarLote(client, "Película Samsung A34 (compra 2)", 5, 1.20m);
        var fabricante = "8" + Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);

        var criado = await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película Samsung A34", "Películas", 999, null, fabricante, "Gaveta P1", null, true, [lote1, lote2]));
        var a = criado.Artigo;
        a.Sku.Should().Be($"LT-{a.Numero:D5}");
        a.CodigoBarras.Should().Be(Ean13.Interno(a.Numero));
        a.Stock.Should().Be(7);
        a.PrecoEfetivoCents.Should().Be(999);
        criado.Lotes.Select(l => l.LinhaId).Should().Equal(lote1, lote2);

        // A pistola lê o código interno, o do fabricante ou o SKU.
        foreach (var codigo in new[] { a.CodigoBarras, fabricante, a.Sku.ToLowerInvariant() })
            (await client.GetFromJsonAsync<ArtigoDto>($"/api/artigos/codigo/{codigo}"))!.Id.Should().Be(a.Id, codigo);
        (await client.GetAsync("/api/artigos/codigo/2999999999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3 unidades: 2 do lote 1 (mais antigo) + 1 do lote 2.
        var venda = await Post<VendaDto>(client, "/api/artigos/balcao/vender",
            new BalcaoVendaRequest([new BalcaoLinhaRequest(a.Id, 3)], PaymentMethod.MBWay));
        venda.Estado.Should().Be(VendaEstado.Entregue);
        venda.Tipo.Should().Be(VendaTipo.Produto);
        venda.TotalCents.Should().Be(2997);
        venda.Items.Select(i => (i.CompraLinhaId, i.Quantidade)).Should().BeEquivalentTo(new[] { ((Guid?)lote1, 2), ((Guid?)lote2, 1) });
        venda.FaturaPorRegistar.Should().BeTrue("até haver fatura-recibo emitida");

        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{a.Id}"))!.Artigo.Stock.Should().Be(4);

        // Mais do que há: recusa sem vender nada.
        var demais = await client.PostAsJsonAsync("/api/artigos/balcao/vender",
            new BalcaoVendaRequest([new BalcaoLinhaRequest(a.Id, 5)], PaymentMethod.Dinheiro));
        demais.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{a.Id}"))!.Artigo.Stock.Should().Be(4);

        // Pesquisa por palavras.
        (await client.GetFromJsonAsync<List<ArtigoDto>>("/api/artigos?q=a34%20samsung"))!.Should().Contain(x => x.Id == a.Id);
    }

    [Fact]
    public async Task SemPrecoDefinido_UsaOCalculadoDoLote_EEtiquetasDaoPdf()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, "Cabo USB-C", 3, 2.00m);
        var a = (await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Cabo USB-C 1m", null, null, null, null, null, null, true, [lote]))).Artigo;
        a.PrecoVendaCents.Should().BeNull();
        a.PrecoEfetivoCents.Should().BeGreaterThan(200, "custo + lucro + IVA");

        var pdf = await client.PostAsJsonAsync("/api/artigos/etiquetas.pdf",
            new EtiquetasRequest([new EtiquetaPedido(a.Id, 3)]));
        pdf.EnsureSuccessStatusCode();
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task CodigoInvalidoOuRepetido_ERecusado()
    {
        var client = await AuthedClient();
        (await client.PostAsJsonAsync("/api/artigos", new ArtigoWriteRequest("X", null, null, "4006381333932", null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var a = (await Post<ArtigoDetalheDto>(client, "/api/artigos", new ArtigoWriteRequest("Artigo A", null, 500, null, null, null, null))).Artigo;
        (await client.PostAsJsonAsync("/api/artigos", new ArtigoWriteRequest("Artigo B", null, null, a.CodigoBarras, null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sugestoes_AgrupamLotesComAMesmaDescricao_EEmLoteCriaJaLigados()
    {
        var client = await AuthedClient();
        var marca = Guid.NewGuid().ToString("N")[..6];
        var l1 = await CriarLote(client, $"Película Teste {marca} A54", 2, 1m);
        var l2 = await CriarLote(client, $"película  teste {marca} a54 ", 3, 1.2m);
        var sug = (await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes"))!.Single(s => s.Nome.Contains(marca, StringComparison.OrdinalIgnoreCase));
        sug.LoteIds.Should().BeEquivalentTo(new[] { l1, l2 });
        sug.Stock.Should().Be(5);
        sug.PrecoCalculadoCents.Should().BeGreaterThan(0);

        var criados = await Post<List<ArtigoDto>>(client, "/api/artigos/em-lote", new ArtigosEmLoteRequest([new ArtigoEmLoteItem(sug.Nome, sug.LoteIds)], "Películas"));
        criados.Single().Stock.Should().Be(5);
        criados.Single().Categoria.Should().Be("Películas");
        (await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes"))!.Should().NotContain(s => s.Nome.Contains(marca, StringComparison.OrdinalIgnoreCase), "já têm artigo");
    }

    [Fact]
    public async Task Talao80mm_DaPdf()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, "Cabo talão", 1, 2m);
        var a = (await Post<ArtigoDetalheDto>(client, "/api/artigos", new ArtigoWriteRequest("Cabo talão", null, 500, null, null, null, null, true, [lote]))).Artigo;
        var venda = await Post<VendaDto>(client, "/api/artigos/balcao/vender", new BalcaoVendaRequest([new BalcaoLinhaRequest(a.Id, 1)], PaymentMethod.Dinheiro));
        var pdf = await client.GetAsync($"/api/vendas/{venda.Id}/talao.pdf");
        pdf.EnsureSuccessStatusCode();
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task PrecoDaCategoria_PeliculasA15_EditavelPorArtigo()
    {
        var client = await AuthedClient();
        var cat = $"Películas {Guid.NewGuid():N}"[..18];
        (await client.PutAsJsonAsync("/api/artigos/categorias", new CategoriaArtigoRequest(cat, 1500))).EnsureSuccessStatusCode();
        var lote = await CriarLote(client, "Vidro temperado teste categoria", 3, 1.5m);
        var geral = (await Post<ArtigoDetalheDto>(client, "/api/artigos", new ArtigoWriteRequest("Película geral", cat, null, null, null, null, null, true, [lote]))).Artigo;
        geral.PrecoEfetivoCents.Should().Be(1500);
        geral.OrigemPreco.Should().Be("categoria");
        var especial = (await Post<ArtigoDetalheDto>(client, "/api/artigos", new ArtigoWriteRequest("Película privacidade", cat, 1999, null, null, null, null))).Artigo;
        especial.PrecoEfetivoCents.Should().Be(1999, "preço próprio ganha ao da categoria");
        especial.OrigemPreco.Should().Be("artigo");

        // O inventário e o balcão usam o preço da categoria.
        var inv = await client.GetFromJsonAsync<List<InventarioLinhaDto>>("/api/compras/inventario");
        inv!.Single(l => l.LinhaId == lote).PrecoVendaArtigo.Should().Be(15m);
        var venda = await Post<VendaDto>(client, "/api/artigos/balcao/vender", new BalcaoVendaRequest([new BalcaoLinhaRequest(geral.Id, 1)], PaymentMethod.Dinheiro));
        venda.TotalCents.Should().Be(1500);

        // Mudar a categoria muda todas as películas sem preço próprio.
        (await client.PutAsJsonAsync("/api/artigos/categorias", new CategoriaArtigoRequest(cat, 1200))).EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{geral.Id}"))!.Artigo.PrecoEfetivoCents.Should().Be(1200);
        (await client.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{especial.Id}"))!.Artigo.PrecoEfetivoCents.Should().Be(1999);
        var cats = await client.GetFromJsonAsync<List<CategoriaArtigoDto>>("/api/artigos/categorias");
        cats!.Single(c => c.Nome == cat).Artigos.Should().Be(2);
    }

    [Fact]
    public async Task Sugestoes_ReconhecemPeliculas()
    {
        var client = await AuthedClient();
        var marca = Guid.NewGuid().ToString("N")[..6];
        await CriarLote(client, $"Vidro temperado Hoco {marca}", 1, 1m);
        await CriarLote(client, $"Bateria {marca}", 1, 5m);
        var sug = await client.GetFromJsonAsync<List<ArtigoSugestaoDto>>("/api/artigos/sugestoes");
        sug!.Single(s => s.Nome.Contains($"Hoco {marca}")).Categoria.Should().Be("Películas");
        sug!.Single(s => s.Nome.Contains($"Bateria {marca}")).Categoria.Should().BeNull();
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var resp = await client.PostAsJsonAsync(url, body);
        if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<Guid> CriarLote(HttpClient client, string descricao, int qtd, decimal preco)
    {
        var forn = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.Nacional, null));
        var fornecedorId = (await forn.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
        var resp = await client.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(fornecedorId, new DateTime(2026, 9, 1),
            "F-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null,
            [new CompraLinhaWriteRequest(null, descricao, qtd, preco, null, null, null)]));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!.Linhas.Single().Id;
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
