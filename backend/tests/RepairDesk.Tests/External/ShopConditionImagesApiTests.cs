using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Services.Shop;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.External;

/// <summary>
/// Sprint 531: guarda de DI + contrato das imagens por estado de condição. Confirma que o endpoint
/// externo (loja) e o admin resolvem pelo contentor real (não 500), e que a loja recebe uma lista.
/// </summary>
public class ShopConditionImagesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ShopConditionImagesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task External_ConditionImages_ResolvesService_Returns200List()
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);

        var resp = await api.GetAsync("/api/external/condition-images");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<ShopConditionImageDto>>();
        list.Should().NotBeNull(); // vazia é válido — a loja faz fallback.
    }

    [Fact]
    public async Task Admin_ListConditionImages_Returns200()
    {
        var jwt = await NewJwtClient();
        var resp = await jwt.GetAsync("/api/shop-condition-images");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_SetInvalidGrade_Returns4xx_NotServerError()
    {
        var jwt = await NewJwtClient();
        using var content = new MultipartFormDataContent();
        var img = new ByteArrayContent(new byte[] { 1, 2, 3 });
        img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(img, "image", "x.png");
        var resp = await jwt.PutAsync("/api/shop-condition-images/c-plus", content);
        // grau inválido (loja só tem a-plus/a/b-plus/b) → 4xx limpo, nunca 500.
        ((int)resp.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);
    }

    private async Task<HttpClient> NewJwtClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> NewApiClient(HttpClient jwt)
    {
        var create = await jwt.PostAsJsonAsync("/api/service-keys", new CreateServiceApiKeyRequest($"condimg-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var resp = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", resp.PlainKey);
        return api;
    }
}
