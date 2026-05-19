using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.External;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.External;

public class ExternalCheckoutApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ExternalCheckoutApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Checkout_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/external/checkout", SampleRequest());
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_WithJwt_Returns401_OnlyApiKeyAllowed()
    {
        // External endpoints rejeitam JWT — só aceitam ApiKey (anti-escalada).
        var client = await NewJwtClient();
        var resp = await client.PostAsJsonAsync("/api/external/checkout", SampleRequest());
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_AfterKeyRevoked_Returns401()
    {
        var (jwtClient, _) = await NewKeyAsync();
        var keys = await jwtClient.GetFromJsonAsync<ServiceApiKeyDto[]>("/api/service-keys");
        var key = keys!.First();

        // Gera 2ª key, revoga, tenta usar a revogada.
        var create = await jwtClient.PostAsJsonAsync("/api/service-keys",
            new CreateServiceApiKeyRequest($"to-revoke-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var newKey = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;

        var revokeResp = await jwtClient.PostAsJsonAsync(
            $"/api/service-keys/{newKey.Key.Id}/revoke",
            new RevokeServiceApiKeyRequest("rotation"));
        revokeResp.EnsureSuccessStatusCode();

        var revokedClient = _factory.CreateClient();
        revokedClient.DefaultRequestHeaders.Add("X-Api-Key", newKey.PlainKey);
        var resp = await revokedClient.PostAsJsonAsync("/api/external/checkout", SampleRequest("511000006"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_HappyPath_CreatesVendaWithGarantia()
    {
        var (_, apiClient) = await NewKeyAsync();

        var req = SampleRequest();
        var resp = await apiClient.PostAsJsonAsync("/api/external/checkout", req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ExternalCheckoutResponse>();

        body.Should().NotBeNull();
        body!.VendaId.Should().NotBeEmpty();
        body.VendaNumero.Should().BeGreaterThan(0);
        body.ClienteCreated.Should().BeTrue();
        body.TotalCents.Should().Be(2000);
        body.GarantiaSlug.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Checkout_SameNifTwice_ReusesCliente()
    {
        var (_, apiClient) = await NewKeyAsync();

        var req = SampleRequest("507000005", "Cliente Idempotente");
        var first = await apiClient.PostAsJsonAsync("/api/external/checkout", req);
        first.EnsureSuccessStatusCode();
        var firstBody = await first.Content.ReadFromJsonAsync<ExternalCheckoutResponse>();

        var second = await apiClient.PostAsJsonAsync("/api/external/checkout", req);
        second.EnsureSuccessStatusCode();
        var secondBody = await second.Content.ReadFromJsonAsync<ExternalCheckoutResponse>();

        firstBody!.ClienteId.Should().Be(secondBody!.ClienteId);  // mesmo cliente
        firstBody.VendaId.Should().NotBe(secondBody.VendaId);     // vendas distintas
        secondBody.ClienteCreated.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrder_ReturnsStatusComGarantia()
    {
        var (_, apiClient) = await NewKeyAsync();
        var checkout = await apiClient.PostAsJsonAsync("/api/external/checkout", SampleRequest("508000009"));
        checkout.EnsureSuccessStatusCode();
        var checkoutBody = (await checkout.Content.ReadFromJsonAsync<ExternalCheckoutResponse>())!;

        var orderResp = await apiClient.GetAsync($"/api/external/orders/{checkoutBody.VendaId}");
        orderResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await orderResp.Content.ReadFromJsonAsync<ExternalOrderStatusResponse>();

        body.Should().NotBeNull();
        body!.Status.Should().Be("Paga");
        body.Origem.Should().Be("Online");
        body.GarantiaSlug.Should().NotBeNullOrEmpty();
        body.GarantiaActiva.Should().BeTrue();
        body.GarantiaAnulada.Should().BeFalse();
    }

    [Fact]
    public async Task CancelOrder_AnulaGarantiaERevertStatus()
    {
        var (_, apiClient) = await NewKeyAsync();
        var checkout = await apiClient.PostAsJsonAsync("/api/external/checkout", SampleRequest("509000002"));
        checkout.EnsureSuccessStatusCode();
        var checkoutBody = (await checkout.Content.ReadFromJsonAsync<ExternalCheckoutResponse>())!;

        var cancelResp = await apiClient.PostAsJsonAsync(
            $"/api/external/orders/{checkoutBody.VendaId}/cancel",
            new CancelOrderRequest("Cliente pediu reembolso 14d"));
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await cancelResp.Content.ReadFromJsonAsync<ExternalOrderStatusResponse>();

        body!.Status.Should().Be("Cancelada");
        body.GarantiaAnulada.Should().BeTrue();
        body.GarantiaActiva.Should().BeFalse();
    }

    [Fact]
    public async Task CancelOrder_Idempotent_TwiceReturnsSameState()
    {
        var (_, apiClient) = await NewKeyAsync();
        var checkout = await apiClient.PostAsJsonAsync("/api/external/checkout", SampleRequest("510000002"));
        checkout.EnsureSuccessStatusCode();
        var checkoutBody = (await checkout.Content.ReadFromJsonAsync<ExternalCheckoutResponse>())!;

        var first = await apiClient.PostAsJsonAsync(
            $"/api/external/orders/{checkoutBody.VendaId}/cancel",
            new CancelOrderRequest(null));
        first.EnsureSuccessStatusCode();

        var second = await apiClient.PostAsJsonAsync(
            $"/api/external/orders/{checkoutBody.VendaId}/cancel",
            new CancelOrderRequest(null));
        second.StatusCode.Should().Be(HttpStatusCode.OK);  // idempotente, não 409

        var body = (await second.Content.ReadFromJsonAsync<ExternalOrderStatusResponse>())!;
        body.Status.Should().Be("Cancelada");
    }

    [Fact]
    public async Task GetHistoricoByNif_ExistingClient_ReturnsVendas()
    {
        var (_, apiClient) = await NewKeyAsync();
        var nif = "512000009";  // 5*9 + 1*8 + 2*7 = 45+8+14 = 67, 67%11=1, cd=10... inválido
        // calcular: 5*9=45, 1*8=8, 2*7=14 → 67. 67%11=1 → cd=11-1=10 → impossível, NIF inicial 5* + dígito 1 não dá
        // usar 503000000 + outro NIF válido
        nif = "503000000";  // já validado em outros tests

        // 1. Cliente novo via checkout
        var checkout = await apiClient.PostAsJsonAsync("/api/external/checkout", SampleRequest(nif));
        checkout.EnsureSuccessStatusCode();

        // 2. Histórico devolve a venda
        var resp = await apiClient.GetAsync($"/api/external/clientes/{nif}/historico");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ExternalClienteHistoricoResponse>();
        body!.Vendas.Should().NotBeEmpty();
        body.Vendas.First().Status.Should().Be("Paga");
    }

    [Fact]
    public async Task GetHistoricoByNif_UnknownNif_Returns404()
    {
        var (_, apiClient) = await NewKeyAsync();
        var resp = await apiClient.GetAsync("/api/external/clientes/999999990/historico");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListParts_AuthorizedViaApiKey_ReturnsCatalog()
    {
        var (_, apiClient) = await NewKeyAsync();
        var resp = await apiClient.GetAsync("/api/external/parts?page=1&pageSize=20");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<PagedResult<ExternalPartDto>>();
        body.Should().NotBeNull();
        // não validamos count exacto — seed pode variar. Validamos shape.
        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListParts_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/external/parts");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/external/health");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_WithApiKey_ReturnsStatusAndTenantId()
    {
        var (_, apiClient) = await NewKeyAsync();
        var resp = await apiClient.GetAsync("/api/external/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ExternalHealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("ok");
        body.ApiVersion.Should().NotBeNullOrEmpty();
        body.TenantId.Should().NotBeNull().And.NotBe(Guid.Empty);
        // serverTime deve estar perto de "agora" (clock skew test).
        (DateTimeOffset.UtcNow - body.ServerTime).Duration().Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    // -- helpers --

    private static ExternalCheckoutRequest SampleRequest(string nif = "504000004", string nome = "Maria Silva")
        => new(
            new ExternalCheckoutCliente(nome, "+351912000000", "test@example.com", nif, null),
            new[]
            {
                new CreateVendaItemRequest(null, "Capa iPhone", 1, 2000, 0, 0m),
            },
            PaymentMethod.MBWay,
            EmitirFatura: false,  // sem provider configurado nos tests
            Notas: "Test external checkout");

    private async Task<HttpClient> NewJwtClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<LoginAuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json!.AccessToken);
        return client;
    }

    /// <summary>Cria uma API key via JWT e devolve (jwtClient, apiClient pré-configurado).</summary>
    private async Task<(HttpClient JwtClient, HttpClient ApiClient)> NewKeyAsync()
    {
        var jwtClient = await NewJwtClient();
        var create = await jwtClient.PostAsJsonAsync("/api/service-keys",
            new CreateServiceApiKeyRequest($"test-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var resp = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;

        var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Api-Key", resp.PlainKey);
        return (jwtClient, apiClient);
    }

    private sealed record LoginAuthResponse(string AccessToken, string RefreshToken);
}
