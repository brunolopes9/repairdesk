using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.DAL.Persistence;
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
        // 1. Cria e paga uma venda: a garantia digital é emitida automaticamente (preferência default).
        var client = await NewAuthedClientAsync();
        var create = await client.PostAsJsonAsync("/api/vendas", new CreateVendaRequest(null,
            new[] { new CreateVendaItemRequest(null, "Película vidro temperado", 1, 1500, 0, 23m) }, "PDF público test"));
        create.EnsureSuccessStatusCode();
        var venda = (await create.Content.ReadFromJsonAsync<VendaDto>())!;
        (await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/marcar-paga", new MarcarVendaPagaRequest(PaymentMethod.MBWay)))
            .EnsureSuccessStatusCode();

        string slug;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            slug = await db.Garantias.IgnoreQueryFilters().Where(g => g.VendaId == venda.Id).Select(g => g.Slug).SingleAsync();
        }

        // 2. PDF público sem auth.
        var publicClient = _factory.CreateClient();
        var resp = await publicClient.GetAsync($"/api/public/warranty/{slug}/pdf");
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

    private async Task<HttpClient> NewAuthedClientAsync()
    {
        var jwtClient = _factory.CreateClient();
        var login = await jwtClient.PostAsJsonAsync("/api/auth/login",
            new { login = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var json = (await login.Content.ReadFromJsonAsync<LoginAuthResponse>())!;
        jwtClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.AccessToken);
        return jwtClient;
    }

    private sealed record LoginAuthResponse(string AccessToken, string RefreshToken);
}
