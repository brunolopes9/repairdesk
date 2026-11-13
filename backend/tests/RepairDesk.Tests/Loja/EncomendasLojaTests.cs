using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Loja;

/// <summary>
/// Sprint 656: encomendas da loja online — preços do servidor, Stripe falso, webhook assinado e idempotente,
/// stock que sai só quando paga, seguimento com token e estados do dono.
/// </summary>
public class EncomendasLojaTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Segredo = "whsec_teste_loja";
    private readonly WebApplicationFactory<Program> _app;

    public EncomendasLojaTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Stripe:WebhookSecret", Segredo);
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IDirectAccessStorage>();
                s.AddSingleton<IDirectAccessStorage, StorageFalso>();
                s.RemoveAll<IStripeCheckout>();
                s.AddSingleton<StripeFalso>();
                s.AddSingleton<IStripeCheckout>(sp => sp.GetRequiredService<StripeFalso>());
            });
        });
    }

    [Fact]
    public async Task Checkout_PagoPeloWebhook_CriaVendaETiraStock_UmaSoVez()
    {
        var admin = await Admin();
        var (produto, artigoId) = await ProdutoComStock(admin, stock: 2, precoCents: 890);
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();

        // O browser manda um preço falso — é ignorado (só slug e quantidade contam).
        var resp = await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 2));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var r = (await resp.Content.ReadFromJsonAsync<CheckoutResultado>())!;
        r.UrlPagamento.Should().StartWith("https://checkout.stripe.test/");
        var stripe = _app.Services.GetRequiredService<StripeFalso>();
        var sessao = stripe.Pedidos.Single(p => p.Referencia == r.Referencia);
        sessao.Linhas.Should().ContainEquivalentOf(new StripeLinha(produto.Titulo, 890, 2));
        sessao.Linhas.Should().ContainEquivalentOf(new StripeLinha("Envio Asfalto Largo (24 h)", 650, 1));
        (await Stock(admin, artigoId)).Should().Be(2, "o stock só sai quando o pagamento é confirmado");

        // Webhook assinado → paga, venda criada, stock sai. Repetido → nada muda.
        var evento = Evento("checkout.session.completed", stripe.SessaoDe(r.Referencia), "paid");
        (await Webhook(anon, evento)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Webhook(anon, evento)).StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = (await admin.GetFromJsonAsync<List<EncomendaLojaDto>>("/api/loja/encomendas"))!;
        var enc = lista.Single(x => x.Referencia == r.Referencia);
        enc.Estado.Should().Be(EncomendaLojaEstado.Paga);
        enc.Total.Should().Be(24.30m);
        enc.VendaId.Should().NotBeNull();
        enc.Problema.Should().BeNull();
        (await Stock(admin, artigoId)).Should().Be(0);
        var venda = (await admin.GetFromJsonAsync<VendaDto>($"/api/vendas/{enc.VendaId}"))!;
        venda.Estado.Should().Be(VendaEstado.Pronta);
        venda.TotalCents.Should().Be(2430);

        // A loja passa a mostrar esgotado.
        (await admin.GetFromJsonAsync<ProdutoLojaDto>($"/api/loja/produtos/{produto.Id}"))!.Disponibilidade.Should().Be(LojaDisponibilidade.Esgotado);

        // Dono: enviada (com nº de seguimento) → entregue (a venda fica entregue).
        await Ok<EncomendaLojaDto>(await admin.PutAsJsonAsync($"/api/loja/encomendas/{enc.Id}/estado",
            new MudarEstadoEncomendaRequest(EncomendaLojaEstado.Enviada, "AL123456")));
        var fim = await Ok<EncomendaLojaDto>(await admin.PutAsJsonAsync($"/api/loja/encomendas/{enc.Id}/estado",
            new MudarEstadoEncomendaRequest(EncomendaLojaEstado.Entregue)));
        fim.EntregueEm.Should().NotBeNull();
        (await admin.GetFromJsonAsync<VendaDto>($"/api/vendas/{enc.VendaId}"))!.Estado.Should().Be(VendaEstado.Entregue);

        // Não volta atrás.
        (await admin.PutAsJsonAsync($"/api/loja/encomendas/{enc.Id}/estado", new MudarEstadoEncomendaRequest(EncomendaLojaEstado.Paga)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Seguimento_PrecisaDoToken_EWebhookSemAssinaturaERecusado()
    {
        var admin = await Admin();
        var (produto, _) = await ProdutoComStock(admin, stock: 1, precoCents: 1290);
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();
        var r = await Ok<CheckoutResultado>(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 1)));
        var stripe = _app.Services.GetRequiredService<StripeFalso>();
        var token = stripe.TokenDe(r.Referencia);

        var ok = await anon.GetFromJsonAsync<EncomendaPublicaDto>($"/api/public/loja/{slug}/encomendas/{r.Referencia}?t={token}");
        ok!.Estado.Should().Be(EncomendaLojaEstado.AguardaPagamento);
        ok.Total.Should().Be(19.40m);
        (await anon.GetAsync($"/api/public/loja/{slug}/encomendas/{r.Referencia}?t=errado")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Sem assinatura válida não se marca nada como pago.
        var falso = new StringContent(Evento("checkout.session.completed", stripe.SessaoDe(r.Referencia), "paid"), Encoding.UTF8, "application/json");
        falso.Headers.Add("Stripe-Signature", "t=1,v1=abc");
        (await anon.PostAsync("/api/public/loja/stripe/webhook", falso)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Expirada.
        (await Webhook(anon, Evento("checkout.session.expired", stripe.SessaoDe(r.Referencia), "unpaid"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anon.GetFromJsonAsync<EncomendaPublicaDto>($"/api/public/loja/{slug}/encomendas/{r.Referencia}?t={token}"))!
            .Estado.Should().Be(EncomendaLojaEstado.Expirada);
    }

    [Fact]
    public async Task Checkout_RecusaQuantidadeAcimaDoStock_ProdutoSemArtigo_EDadosInvalidos()
    {
        var admin = await Admin();
        var (produto, _) = await ProdutoComStock(admin, stock: 1, precoCents: 990);
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();

        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 2))).StatusCode).Should().BeOneOf(400, 422);
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido("nao-existe", 1))).StatusCode).Should().BeOneOf(400, 422);
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 1) with { CodigoPostal = "3500" })).StatusCode).Should().BeOneOf(400, 422);
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 1) with { AceitaTermos = false })).StatusCode).Should().BeOneOf(400, 422);
        (await anon.PostAsJsonAsync("/api/public/loja/slug-que-nao-existe/checkout", Pedido(produto.Slug, 1))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Bot (campo-armadilha).
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 1) with { Website = "http://spam" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- helpers ----------

    private static CheckoutRequest Pedido(string slug, int qtd) => new(
        new[] { new CheckoutLinhaRequest(slug, qtd) }, "Maria Teste Silva", "maria@example.com", "912 345 678", null,
        "Rua da Igreja 1", "3505-347", "Viseu", null, true);

    private async Task<(ProdutoLojaDto Produto, Guid ArtigoId)> ProdutoComStock(HttpClient admin, int stock, int precoCents)
    {
        var forn = await Ok<FornecedorDto>(await admin.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.UeAutoliquidacao, "NL")));
        var nome = "Artigo loja " + Guid.NewGuid().ToString("N")[..8];
        var compra = await Ok<CompraDocumentoDto>(await admin.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(forn.Id,
            new DateTime(2026, 11, 1), "I2026-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null,
            new[] { new CompraLinhaWriteRequest(null, nome, stock, 2m, null, null, null) })));
        var artigo = await Ok<ArtigoDetalheDto>(await admin.PostAsJsonAsync("/api/artigos",
            new ArtigoWriteRequest(nome, "Acessórios", precoCents, null, null, null, null, true, [compra.Linhas.Single().Id])));
        var resp = await admin.PostAsJsonAsync("/api/loja/produtos", new GuardarProdutoLojaRequest(
            "[ENC] " + nome, "Rixus", null, LojaCategoria.Acessorios, LojaEstado.Novo, null, 36, LojaRegimeIva.Normal, LojaOrigem.Proprio,
            null, null, 1, null, null, null, null, null, null, null, null, null, ArtigoId: artigo.Artigo.Id));
        var p = await Ok<ProdutoLojaDto>(resp);
        var t = await Ok<UploadFotoTicketDto>(await admin.PostAsJsonAsync($"/api/loja/produtos/{p.Id}/fotos/upload",
            new PedirUploadFotoRequest("a.jpg", "image/jpeg", 1000, null)));
        await Ok<ProdutoLojaDto>(await admin.PostAsync($"/api/loja/produtos/{p.Id}/fotos/{t.FotoId}/confirmar", null));
        p = await Ok<ProdutoLojaDto>(await admin.PostAsync($"/api/loja/produtos/{p.Id}/publicar", null));
        return (p, artigo.Artigo.Id);
    }

    private static async Task<int> Stock(HttpClient admin, Guid artigoId)
        => (await admin.GetFromJsonAsync<ArtigoDetalheDto>($"/api/artigos/{artigoId}"))!.Artigo.Stock;

    private static async Task<string> IntakeSlug(HttpClient admin)
    {
        var resp = await admin.GetAsync("/api/automacoes/intake-widget");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IntakeWidgetDto>())!.Slug;
    }

    private sealed record IntakeWidgetDto(string Slug, string? PublicUrl);

    private static string Evento(string tipo, string sessao, string paymentStatus)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = tipo,
            data = new { @object = new { @object = "checkout.session", id = sessao, payment_status = paymentStatus, payment_intent = "pi_teste" } },
        });

    private static Task<HttpResponseMessage> Webhook(HttpClient client, string payload)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", $"t={ts},v1={StripeWebhook.Assinar(payload, ts, Segredo)}");
        return client.PostAsync("/api/public/loja/stripe/webhook", content);
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

    /// <summary>Stripe em memória: guarda o pedido e devolve uma sessão falsa.</summary>
    public sealed class StripeFalso : IStripeCheckout
    {
        public List<StripeSessaoPedido> Pedidos { get; } = new();
        public bool Configurado => true;

        public Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default)
        {
            lock (Pedidos) Pedidos.Add(pedido);
            return Task.FromResult(new StripeSessao($"cs_test_{pedido.EncomendaId:N}", $"https://checkout.stripe.test/{pedido.EncomendaId:N}"));
        }

        public string SessaoDe(string referencia) { lock (Pedidos) return $"cs_test_{Pedidos.Single(p => p.Referencia == referencia).EncomendaId:N}"; }

        /// <summary>O token do link de seguimento vai no URL de sucesso.</summary>
        public string TokenDe(string referencia)
        {
            string url;
            lock (Pedidos) url = Pedidos.Single(p => p.Referencia == referencia).UrlSucesso;
            var t = url.Split("&t=")[1];
            return t.Split('&')[0];
        }
    }

    private sealed class StorageFalso : IDirectAccessStorage
    {
        public string CreateUploadUrl(string key, string contentType, TimeSpan validFor) => $"https://r2.falso/{key}?put";
        public string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null) => $"https://r2.falso/{key}";
        public Task<long?> GetSizeAsync(string key, CancellationToken ct = default) => Task.FromResult<long?>(1000);
        public Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default) => Task.CompletedTask;
    }
}
