using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Loja;
using RepairDesk.Tests.Auth;
using static RepairDesk.Tests.Loja.EncomendasLojaTests;

namespace RepairDesk.Tests.Loja;

/// <summary>
/// Sprint 690: a loja cobra pelo ifthenpay (Pay by Link). O ifthenpay é simulado ao nível do HTTP — o código real
/// (router, pedido ao /gateway/pinpay/{key}, callback com a chave anti-phishing) corre todo.
/// </summary>
public class IfthenpayLojaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;
    private readonly IfthenpayFalso _ifthenpay = new();

    public IfthenpayLojaTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IDirectAccessStorage>();
            s.AddSingleton<IDirectAccessStorage, StorageFalso>();
            s.RemoveAll<IStripeCheckout>();
            s.AddScoped<IStripeCheckout, PagamentoLojaRouter>();
            s.AddHttpClient<IfthenpayLojaCheckout>().ConfigurePrimaryHttpMessageHandler(() => _ifthenpay);
        }));
    }

    [Fact]
    public async Task Ifthenpay_ChavesNasDefinicoes_CheckoutCriaLink_CallbackConfirmaUmaSoVez()
    {
        var admin = await Admin();
        var (produto, artigoId) = await ProdutoComStock(admin, stock: 2, precoCents: 890);
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();

        // Sem chaves: ainda não dá para pagar (nem Stripe no servidor de testes).
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 1))).StatusCode.Should().NotBe(HttpStatusCode.OK);

        // As chaves entram em Definições e nunca voltam ao browser.
        var estado = await Ok<PagamentosLojaEstadoDto>(await admin.PutAsJsonAsync("/api/loja/pagamentos",
            new GuardarPagamentosLojaRequest("ABC-123456", "chave-anti-phishing-teste")));
        estado.Ativo.Should().BeTrue();
        estado.CallbackUrl.Should().Contain($"/api/public/loja/{slug}/ifthenpay/callback?key=[ANTI_PHISHING_KEY]");
        (await admin.GetStringAsync("/api/loja/pagamentos")).Should().NotContain("ABC-123456").And.NotContain("chave-anti-phishing-teste");
        (await anon.GetAsync("/api/loja/pagamentos")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Sprint 700: Gateway Key «dinâmica» — sem métodos a página do ifthenpay abria vazia. As chaves de cada
        // método entram cifradas; só os códigos voltam. Método desconhecido ou chave com «|» → 422.
        estado = await Ok<PagamentosLojaEstadoDto>(await admin.PutAsJsonAsync("/api/loja/pagamentos",
            new GuardarPagamentosLojaRequest(null, null, Metodos: new() { ["MBWAY"] = "MBW-111111", ["MB"] = "MBK-222222", ["CCARD"] = "CCK-333333" })));
        estado.MetodosDefinidos.Should().Equal("MB", "MBWAY", "CCARD");
        (await admin.GetStringAsync("/api/loja/pagamentos")).Should().NotContain("MBW-111111").And.NotContain("CCK-333333");
        (await admin.PutAsJsonAsync("/api/loja/pagamentos", new GuardarPagamentosLojaRequest(null, null, Metodos: new() { ["BITCOIN"] = "X-1" })))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await admin.PutAsJsonAsync("/api/loja/pagamentos", new GuardarPagamentosLojaRequest(null, null, Metodos: new() { ["MB"] = "A|B;C" })))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        // «-» tira um método; vazio não mexe.
        estado = await Ok<PagamentosLojaEstadoDto>(await admin.PutAsJsonAsync("/api/loja/pagamentos",
            new GuardarPagamentosLojaRequest(null, null, Metodos: new() { ["CCARD"] = "-", ["MB"] = "" })));
        estado.MetodosDefinidos.Should().Equal("MB", "MBWAY");

        // Checkout → pedido ao ifthenpay com o total do servidor e a referência como id.
        var r = await Ok<CheckoutResultado>(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/checkout", Pedido(produto.Slug, 2)));
        r.UrlPagamento.Should().Be("https://gateway.ifthenpay.com/url/teste");
        _ifthenpay.UltimoUrl.Should().EndWith("/gateway/pinpay/ABC-123456");
        _ifthenpay.UltimoCorpo!.RootElement.GetProperty("id").GetString().Should().Be(r.Referencia);
        _ifthenpay.UltimoCorpo.RootElement.GetProperty("amount").GetString().Should().Be("22.70"); // 2 × 8,90 + 4,90 € CTT
        _ifthenpay.UltimoCorpo.RootElement.GetProperty("accounts").GetString().Should().Be("MB|MBK-222222;MBWAY|MBW-111111");
        (await Stock(admin, artigoId)).Should().Be(2);

        string Cb(string key, string amount) =>
            $"/api/public/loja/{slug}/ifthenpay/callback?key={key}&id={r.Referencia}&amount={amount}&payment_datetime=18-11-2026%2015:00:00&payment_method=MBWAY";

        // Chave errada → 403, nada muda. Valor errado → recebido mas NÃO pago.
        (await anon.GetAsync(Cb("errada", "22.70"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anon.GetAsync(Cb("chave-anti-phishing-teste", "1.00"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Estado(admin, r.Referencia)).Should().Be(EncomendaLojaEstado.AguardaPagamento);

        // Callback certo (repetido, como o ifthenpay faz se não receber 200) → paga uma vez, stock sai uma vez.
        (await anon.GetAsync(Cb("chave-anti-phishing-teste", "22.70"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anon.GetAsync(Cb("chave-anti-phishing-teste", "22.70"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Estado(admin, r.Referencia)).Should().Be(EncomendaLojaEstado.Paga);
        (await Stock(admin, artigoId)).Should().Be(0);

        // Loja desconhecida → 403 (não revela nada).
        (await anon.GetAsync($"/api/public/loja/nao-existe/ifthenpay/callback?key=x&id={r.Referencia}&amount=22.80")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<EncomendaLojaEstado> Estado(HttpClient admin, string referencia)
        => (await admin.GetFromJsonAsync<List<EncomendaLojaDto>>("/api/loja/encomendas"))!.Single(x => x.Referencia == referencia).Estado;

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

    /// <summary>O servidor do ifthenpay, em memória: guarda o pedido e devolve um RedirectUrl.</summary>
    private sealed class IfthenpayFalso : HttpMessageHandler
    {
        public string? UltimoUrl { get; private set; }
        public JsonDocument? UltimoCorpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimoUrl = request.RequestUri!.ToString();
            UltimoCorpo = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { PinCode = 123456789, RedirectUrl = "https://gateway.ifthenpay.com/url/teste", PinpayUrl = "https://pinpay.pt/123456789" }),
            };
        }
    }
}
