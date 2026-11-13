using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Loja;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Loja;

/// <summary>
/// Sprint 613 (Doc 97): produtos da loja — regras legais (garantia mínima, regime da margem),
/// cálculo do IVA/lucro, publicação com fotos e catálogo para o site sem custos.
/// </summary>
public class LojaApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;

    public LojaApiTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IDirectAccessStorage>();
            s.AddSingleton<IDirectAccessStorage, StorageFalso>();
        }));
    }

    [Fact]
    public async Task RegimeDaMargem_CalculaIvaSoSobreAMargem()
    {
        var client = await Admin();
        // iPhone comprado a 100 €, vendido a 250 €: IVA = 150 × 23/123 = 28,05 €.
        var p = await Criar(client, Iphone("iPhone 12 Pro Max 256GB Teste Margem") with { Preco = 250, Custo = 100 });

        p.Numero.Should().BeGreaterThan(0);
        p.Slug.Should().Be("iphone-12-pro-max-256gb-teste-margem");
        p.IvaEstimado.Should().Be(28.05m);
        p.LucroEstimado.Should().Be(121.95m);
        p.UrlSite.Should().Be("https://lopestech.pt/loja/smartphones/iphone-12-pro-max-256gb-teste-margem");
    }

    [Fact]
    public async Task RegrasLegais_GarantiaMinimaEMargemSoEmUsados()
    {
        var client = await Admin();
        // Novo de caixa aberta continua a ser bem novo: 3 anos ao consumidor.
        (await client.PostAsJsonAsync("/api/loja/produtos", Portatil("Caixa aberta 18m") with { GarantiaMeses = 18 }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        // Usado pode ir a 18 meses.
        (await client.PostAsJsonAsync("/api/loja/produtos", Portatil("Usado 18m") with { Estado = LojaEstado.Usado, GarantiaMeses = 18 }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        // Margem só em bens em segunda mão.
        (await client.PostAsJsonAsync("/api/loja/produtos", Portatil("Novo margem") with { RegimeIva = LojaRegimeIva.Margem }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        // Recondicionado com 18 meses: não.
        (await client.PostAsJsonAsync("/api/loja/produtos", Iphone("Recond 18m") with { GarantiaMeses = 18 }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publicar_PrecisaDeFoto_EDepoisOEnderecoNaoMuda()
    {
        var client = await Admin();
        var p = await Criar(client, Portatil("Lenovo Publicar Teste"));

        (await client.PostAsync($"/api/loja/produtos/{p.Id}/publicar", null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var ticket = await Ok<UploadFotoTicketDto>(await client.PostAsJsonAsync($"/api/loja/produtos/{p.Id}/fotos/upload",
            new PedirUploadFotoRequest("frente.jpg", "image/jpeg", 120_000, "Frente do portátil")));
        ticket.UploadUrl.Should().StartWith("https://r2.falso/loja/");
        await Ok<ProdutoLojaDto>(await client.PostAsync($"/api/loja/produtos/{p.Id}/fotos/{ticket.FotoId}/confirmar", null));

        var publicado = await Ok<ProdutoLojaDto>(await client.PostAsync($"/api/loja/produtos/{p.Id}/publicar", null));
        publicado.Publicado.Should().BeTrue();

        (await client.PutAsJsonAsync($"/api/loja/produtos/{p.Id}", Portatil("Lenovo Publicar Teste") with { Slug = "outro-endereco" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"/api/loja/produtos/{p.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Catalogo_SoPublicados_SemCustos_EComScopeLoja()
    {
        var admin = await Admin();
        var publicado = await Criar(admin, Portatil("Catalogo Publicado Teste") with { Custo = 219 });
        await PublicarComFoto(admin, publicado.Id);
        var rascunho = await Criar(admin, Portatil("Catalogo Rascunho Teste"));

        var loja = await ApiKeyClient(admin, "loja");
        var resp = await loja.GetAsync("/api/external/loja/catalogo");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        json.Should().NotContainAny("custo", "Custo", "lucro", "Lucro", "21900"); // 219 € em cêntimos (o hash da versão pode ter «219» por acaso)
        var cat = (await resp.Content.ReadFromJsonAsync<CatalogoDto>())!;
        cat.Produtos.Should().Contain(x => x.Slug == publicado.Slug && x.Categoria == "computadores"
            && x.CondicaoSchema == "https://schema.org/NewCondition" && x.Disponibilidade == "PorEncomenda" && x.Fotos.Count == 1);
        cat.Produtos.Should().NotContain(x => x.Slug == rascunho.Slug);

        // A versão não muda se nada mudou (o gerador só reconstrói quando muda).
        var outra = (await (await loja.GetAsync("/api/external/loja/catalogo")).Content.ReadFromJsonAsync<CatalogoDto>())!;
        outra.Versao.Should().Be(cat.Versao);

        var semScope = await ApiKeyClient(admin, "conteudo");
        (await semScope.GetAsync("/api/external/loja/catalogo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Importar_CriaRascunhos_ENaoDuplica()
    {
        var admin = await Admin();
        var lista = new[] { Portatil("Import Montra A"), Portatil("Import Montra B") };
        var r1 = (await (await admin.PostAsJsonAsync("/api/loja/produtos/importar", lista)).Content.ReadFromJsonAsync<ImportarProdutosResultado>())!;
        r1.Criados.Should().Be(2);
        r1.Produtos.Should().OnlyContain(p => !p.Publicado);
        var r2 = (await (await admin.PostAsJsonAsync("/api/loja/produtos/importar", lista)).Content.ReadFromJsonAsync<ImportarProdutosResultado>())!;
        r2.Criados.Should().Be(0);
        r2.JaExistiam.Should().Be(2);
    }

    [Fact]
    public async Task LigadoAoArtigo_PrecoEStockVemSempreDoArtigo()
    {
        var admin = await Admin();
        var nome = "Protetor camara teste " + Guid.NewGuid().ToString("N")[..6];
        var artigo = await Ok<ArtigoDetalheDto>(await admin.PostAsJsonAsync("/api/artigos",
            new ArtigoWriteRequest(nome, "Acessórios", 890, null, null, null, null, true, [])));
        var id = artigo.Artigo.Id;

        // O preço escrito à mão (15 €) é ignorado: vale o do artigo (8,90 €). Sem stock → esgotado.
        var p = await Criar(admin, Iphone("[LJ] " + nome) with
        {
            Estado = LojaEstado.Novo, Grau = null, RegimeIva = LojaRegimeIva.Normal, Categoria = LojaCategoria.Pecas, Preco = 15, ArtigoId = id,
        });
        p.Preco.Should().Be(8.90m);
        p.ArtigoId.Should().Be(id);
        p.ArtigoNumero.Should().Be(artigo.Artigo.Numero);
        p.Unidades.Should().Be(0);
        p.Disponibilidade.Should().Be(LojaDisponibilidade.Esgotado);
        p.UrlSite.Should().Contain("/loja/pecas/");

        // Mudar o preço no artigo muda a loja, sem tocar no produto.
        await Ok<ArtigoDetalheDto>(await admin.PutAsJsonAsync($"/api/artigos/{id}",
            new ArtigoWriteRequest(nome, "Acessórios", 990, artigo.Artigo.CodigoBarras, null, null, null, true, [])));
        (await admin.GetFromJsonAsync<ProdutoLojaDto>($"/api/loja/produtos/{p.Id}"))!.Preco.Should().Be(9.90m);

        // O mesmo artigo não pode estar em dois produtos.
        var dup = await admin.PostAsJsonAsync("/api/loja/produtos", Iphone("[LJ] dup " + nome) with
            { Estado = LojaEstado.Novo, Grau = null, RegimeIva = LojaRegimeIva.Normal, ArtigoId = id });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Artigo inexistente → erro claro.
        var mau = await admin.PostAsJsonAsync("/api/loja/produtos", Iphone("[LJ] mau " + nome) with
            { Estado = LojaEstado.Novo, Grau = null, RegimeIva = LojaRegimeIva.Normal, ArtigoId = Guid.NewGuid() });
        ((int)mau.StatusCode).Should().BeOneOf(400, 422);
    }

    [Fact]
    public async Task Catalogo_LevaOEanDoFabricante_SoQuandoEValido()
    {
        var admin = await Admin();
        var nome = "Cabo gtin teste " + Guid.NewGuid().ToString("N")[..6];
        // 5901234123457 é um EAN-13 válido de exemplo (dígito de controlo certo).
        var artigo = await Ok<ArtigoDetalheDto>(await admin.PostAsJsonAsync("/api/artigos",
            new ArtigoWriteRequest(nome, "Acessórios", 890, null, "5901234123457", null, null, true, [])));
        var p = await Criar(admin, Iphone("[LJ] " + nome) with { Estado = LojaEstado.Novo, Grau = null, RegimeIva = LojaRegimeIva.Normal, ArtigoId = artigo.Artigo.Id });
        await PublicarComFoto(admin, p.Id);

        var loja = await ApiKeyClient(admin, "loja");
        var cat = (await (await loja.GetAsync("/api/external/loja/catalogo")).Content.ReadFromJsonAsync<CatalogoDto>())!;
        cat.Produtos.Single(x => x.Slug == p.Slug).Gtin.Should().Be("5901234123457");
        ArtigoService.GtinFabricante(new RepairDesk.Core.Entities.Artigo { Nome = "x", Sku = "x", CodigoBarras = "2000000001173" }).Should().BeNull("o código interno da loja não é um EAN de fabricante");
    }

    // ---------- helpers ----------

    private static GuardarProdutoLojaRequest Portatil(string titulo) => new(
        $"[LJ] {titulo}", "Lenovo", "IdeaPad Flex 5 14ARE05", LojaCategoria.Computadores, LojaEstado.NovoCaixaAberta, null, 36,
        LojaRegimeIva.Normal, LojaOrigem.Fornecedor, null, null, 509, null, "Cinzento", 8, 128, "Portátil 2 em 1.",
        new[] { new SpecLoja("Processador", "AMD Ryzen 3 4300U") }, null, null, null);

    private static GuardarProdutoLojaRequest Iphone(string titulo) => new(
        titulo, "Apple", "iPhone 12 Pro Max", LojaCategoria.Smartphones, LojaEstado.Recondicionado, LojaGrau.MuitoBom, 36,
        LojaRegimeIva.Margem, LojaOrigem.Proprio, null, null, 250, 100, "Azul", 6, 256, null, null, null, null, null);

    private static async Task<ProdutoLojaDto> Criar(HttpClient client, GuardarProdutoLojaRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/loja/produtos", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<ProdutoLojaDto>())!;
    }

    private static async Task PublicarComFoto(HttpClient client, Guid id)
    {
        var t = await Ok<UploadFotoTicketDto>(await client.PostAsJsonAsync($"/api/loja/produtos/{id}/fotos/upload",
            new PedirUploadFotoRequest("a.jpg", "image/jpeg", 1000, null)));
        await Ok<ProdutoLojaDto>(await client.PostAsync($"/api/loja/produtos/{id}/fotos/{t.FotoId}/confirmar", null));
        await Ok<ProdutoLojaDto>(await client.PostAsync($"/api/loja/produtos/{id}/publicar", null));
    }

    private static async Task<T> Ok<T>(HttpResponseMessage resp)
    {
        resp.IsSuccessStatusCode.Should().BeTrue(await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpClient> Admin()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> ApiKeyClient(HttpClient admin, string scope)
    {
        var resp = await admin.PostAsJsonAsync("/api/service-keys",
            new CreateServiceApiKeyRequest($"[LJ] chave {scope} {Guid.NewGuid():N}", new[] { scope }));
        resp.EnsureSuccessStatusCode();
        var created = (await resp.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", created.PlainKey);
        return client;
    }

    private sealed class StorageFalso : IDirectAccessStorage
    {
        public string CreateUploadUrl(string key, string contentType, TimeSpan validFor) => $"https://r2.falso/{key}?put";
        public string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null) => $"https://r2.falso/{key}";
        public Task<long?> GetSizeAsync(string key, CancellationToken ct = default) => Task.FromResult<long?>(1000);
        public Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default) => Task.CompletedTask;
    }
}
