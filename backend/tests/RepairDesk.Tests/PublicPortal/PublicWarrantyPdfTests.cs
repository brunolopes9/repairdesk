using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.Core.Enums;
using RepairDesk.Services.External;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.PublicPortal;

public class PublicWarrantyPdfTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PublicWarrantyPdfTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Pdf_SlugDoesNotExist_Returns404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/public/warranty/slug-inexistente-zzz/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pdf_ValidSlug_ReturnsPdfBytes()
    {
        // 1. Cria venda via checkout external para garantir garantia + slug.
        var apiClient = await NewApiKeyClientAsync();
        var checkout = await apiClient.PostAsJsonAsync("/api/external/checkout", new ExternalCheckoutRequest(
            new ExternalCheckoutCliente("Cliente PDF", "+351912000000", "pdf@test.example", "504000004", null),
            new[] { new CreateVendaItemRequest(null, "iPhone 12 Refurbished", 1, 30000, 0, 0m) },
            PaymentMethod.MBWay,
            EmitirFatura: false,
            Notas: "PDF público test"));
        checkout.EnsureSuccessStatusCode();
        var body = (await checkout.Content.ReadFromJsonAsync<ExternalCheckoutResponse>())!;
        body.GarantiaSlug.Should().NotBeNullOrEmpty();

        // 2. PDF público sem auth.
        var publicClient = _factory.CreateClient();
        var resp = await publicClient.GetAsync($"/api/public/warranty/{body.GarantiaSlug}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000); // PDF mínimo razoável
        // PDF assinatura: %PDF
        bytes[0].Should().Be(0x25);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x44);
        bytes[3].Should().Be(0x46);
    }

    private async Task<HttpClient> NewApiKeyClientAsync()
    {
        var jwtClient = _factory.CreateClient();
        var login = await jwtClient.PostAsJsonAsync("/api/auth/login",
            new { email = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var json = (await login.Content.ReadFromJsonAsync<LoginAuthResponse>())!;
        jwtClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.AccessToken);

        var create = await jwtClient.PostAsJsonAsync("/api/service-keys",
            new CreateServiceApiKeyRequest($"pdf-test-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var key = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;

        var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Api-Key", key.PlainKey);
        return apiClient;
    }

    private sealed record LoginAuthResponse(string AccessToken, string RefreshToken);
}
