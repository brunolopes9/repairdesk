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
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Loja;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Loja;

/// <summary>Sprint 659: contas de cliente da loja — registo, login, bloqueio, sessão, encomendas da própria conta.</summary>
public class ContasLojaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;

    public ContasLojaTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Stripe:WebhookSecret", "whsec_teste");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IDirectAccessStorage>();
                s.AddSingleton<IDirectAccessStorage, StorageFalso>();
                s.RemoveAll<IStripeCheckout>();
                s.AddSingleton<IStripeCheckout, EncomendasLojaTests.StripeFalso>();
            });
        });
    }

    [Fact]
    public async Task Registar_Entrar_Sair_EBloqueioAoFimDe5Falhas()
    {
        var slug = await IntakeSlug(await Admin());
        var anon = _app.CreateClient();
        var email = $"cliente{Guid.NewGuid():N}@example.com";

        var reg = await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/registar", new RegistoLojaRequest("Ana Teste", email.ToUpperInvariant(), "912345678", "segredo123", true));
        reg.StatusCode.Should().Be(HttpStatusCode.OK, await reg.Content.ReadAsStringAsync());
        var s1 = (await reg.Content.ReadFromJsonAsync<SessaoLojaDto>())!;
        s1.Cliente.Email.Should().Be(email, "o email guarda-se em minúsculas");

        // Mesmo email outra vez → conflito.
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/registar", new RegistoLojaRequest("Outra", email, null, "segredo123", true)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Palavra-passe curta → 422.
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/registar", new RegistoLojaRequest("X", "x@example.com", null, "123", true))).StatusCode).Should().BeOneOf(400, 422);

        (await Eu(anon, slug, s1.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Eu(anon, slug, "token-falso")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Sair invalida a sessão.
        var sair = new HttpRequestMessage(HttpMethod.Post, $"/api/public/loja/{slug}/conta/sair");
        sair.Headers.Add("X-Loja-Sessao", s1.Token);
        (await anon.SendAsync(sair)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Eu(anon, slug, s1.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Entrar com a palavra-passe certa funciona; 5 erradas bloqueiam (mesmo com a certa a seguir).
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/entrar", new EntrarLojaRequest(email, "segredo123"))).StatusCode.Should().Be(HttpStatusCode.OK);
        for (var i = 0; i < 5; i++)
            ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/entrar", new EntrarLojaRequest(email, "errada!!"))).StatusCode).Should().BeOneOf(400, 422);
        var bloq = await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/entrar", new EntrarLojaRequest(email, "segredo123"));
        ((int)bloq.StatusCode).Should().BeOneOf(400, 422);
        (await bloq.Content.ReadAsStringAsync()).Should().Contain("bloqueado");

        // Recuperar nunca revela se o email existe.
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/conta/recuperar", new PedirResetLojaRequest("nao-existe@example.com"))).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task EncomendaFeitaComSessao_FicaNaConta_EOutraContaNaoAVe()
    {
        var admin = await Admin();
        var slug = await IntakeSlug(admin);
        var produto = await ProdutoComStock(admin);
        var anon = _app.CreateClient();
        var a = await Registar(anon, slug);
        var b = await Registar(anon, slug);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/public/loja/{slug}/checkout")
        {
            Content = JsonContent.Create(new CheckoutRequest(new[] { new CheckoutLinhaRequest(produto, 1) }, "Ana Teste", "ana@example.com",
                "912345678", null, "Rua 1", "3500-001", "Viseu", null, true)),
        };
        req.Headers.Add("X-Loja-Sessao", a.Token);
        var r = await anon.SendAsync(req);
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var refe = (await r.Content.ReadFromJsonAsync<CheckoutResultado>())!.Referencia;

        (await Encomendas(anon, slug, a.Token)).Should().ContainSingle(e => e.Referencia == refe);
        (await Encomendas(anon, slug, b.Token)).Should().BeEmpty();
        var semSessao = await anon.GetAsync($"/api/public/loja/{slug}/conta/encomendas");
        semSessao.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- helpers ----------

    private async Task<SessaoLojaDto> Registar(HttpClient c, string slug)
    {
        var r = await c.PostAsJsonAsync($"/api/public/loja/{slug}/conta/registar",
            new RegistoLojaRequest("Cliente Teste", $"c{Guid.NewGuid():N}@example.com", null, "segredo123", true));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<SessaoLojaDto>())!;
    }

    private static Task<HttpResponseMessage> Eu(HttpClient c, string slug, string token)
    {
        var m = new HttpRequestMessage(HttpMethod.Get, $"/api/public/loja/{slug}/conta/eu");
        m.Headers.Add("X-Loja-Sessao", token);
        return c.SendAsync(m);
    }

    private static async Task<List<EncomendaPublicaDto>> Encomendas(HttpClient c, string slug, string token)
    {
        var m = new HttpRequestMessage(HttpMethod.Get, $"/api/public/loja/{slug}/conta/encomendas");
        m.Headers.Add("X-Loja-Sessao", token);
        var r = await c.SendAsync(m);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<List<EncomendaPublicaDto>>())!;
    }

    private async Task<string> ProdutoComStock(HttpClient admin)
    {
        var forn = (await (await admin.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.UeAutoliquidacao, "NL"))).Content.ReadFromJsonAsync<FornecedorDto>())!;
        var nome = "Artigo conta " + Guid.NewGuid().ToString("N")[..8];
        var compra = (await (await admin.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(forn.Id, new DateTime(2026, 11, 1),
            "I2026-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null,
            new[] { new CompraLinhaWriteRequest(null, nome, 2, 2m, null, null, null) }))).Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        var artigo = (await (await admin.PostAsJsonAsync("/api/artigos",
            new ArtigoWriteRequest(nome, "Acessórios", 990, null, null, null, null, true, [compra.Linhas.Single().Id]))).Content.ReadFromJsonAsync<ArtigoDetalheDto>())!;
        var p = (await (await admin.PostAsJsonAsync("/api/loja/produtos", new GuardarProdutoLojaRequest("[CT] " + nome, "Rixus", null, LojaCategoria.Acessorios,
            LojaEstado.Novo, null, 36, LojaRegimeIva.Normal, LojaOrigem.Proprio, null, null, 1, null, null, null, null, null, null, null, null, null,
            ArtigoId: artigo.Artigo.Id))).Content.ReadFromJsonAsync<ProdutoLojaDto>())!;
        var t = (await (await admin.PostAsJsonAsync($"/api/loja/produtos/{p.Id}/fotos/upload", new PedirUploadFotoRequest("a.jpg", "image/jpeg", 1000, null))).Content.ReadFromJsonAsync<UploadFotoTicketDto>())!;
        (await admin.PostAsync($"/api/loja/produtos/{p.Id}/fotos/{t.FotoId}/confirmar", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/loja/produtos/{p.Id}/publicar", null)).EnsureSuccessStatusCode();
        return p.Slug;
    }

    private static async Task<string> IntakeSlug(HttpClient admin)
    {
        var resp = await admin.GetAsync("/api/automacoes/intake-widget");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IntakeWidgetDto>())!.Slug;
    }

    private sealed record IntakeWidgetDto(string Slug, string? PublicUrl);

    private async Task<HttpClient> Admin()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
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
