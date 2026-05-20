using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Parts;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Webhooks;

/// <summary>
/// Sprint 125: a loja online consome estes eventos para invalidar a cache do read replica.
/// Só disparam quando MostrarLojaOnline=true — produtos internos não vazam para o catálogo público.
/// </summary>
public class CatalogWebhookEventsTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public CatalogWebhookEventsTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreatePart_WithMostrarLojaOnline_PublishesPartsAdicionado()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, WebhookEvents.PartsAdicionado);

        var sku = "WHK-ADD-" + Guid.NewGuid().ToString("N")[..6];
        await CreatePartAsync(client, sku, mostrarLojaOnline: true);

        var delivery = await AssertSingleDeliveryAsync(WebhookEvents.PartsAdicionado, sku);
        delivery.PayloadJson.Should().Contain(sku);
    }

    [Fact]
    public async Task CreatePart_WithoutMostrarLojaOnline_DoesNotPublish()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, WebhookEvents.PartsAdicionado);

        var sku = "WHK-INT-" + Guid.NewGuid().ToString("N")[..6];
        await CreatePartAsync(client, sku, mostrarLojaOnline: false);

        await AssertNoDeliveryAsync(WebhookEvents.PartsAdicionado, sku);
    }

    [Fact]
    public async Task UpdatePart_TogglingMostrarLojaOnlineOff_PublishesPartsRemovido()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, WebhookEvents.PartsRemovido);

        var sku = "WHK-REM-" + Guid.NewGuid().ToString("N")[..6];
        var part = await CreatePartAsync(client, sku, mostrarLojaOnline: true);

        var upd = await client.PutAsJsonAsync($"/api/parts/{part.Id}", new UpdatePartRequest(
            part.Sku, part.Nome, part.Categoria, part.Marca, part.Modelo, part.PriceTableEntryId,
            part.QtdStock, part.QtdMinima, part.CustoUnitarioCents,
            part.Fornecedor, part.LocalArmazenamento, part.Notas,
            Activo: true, MostrarLojaOnline: false));
        upd.EnsureSuccessStatusCode();

        await AssertSingleDeliveryAsync(WebhookEvents.PartsRemovido, sku);
    }

    [Fact]
    public async Task DeletePart_WhileInCatalog_PublishesPartsRemovido()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, WebhookEvents.PartsRemovido);

        var sku = "WHK-DEL-" + Guid.NewGuid().ToString("N")[..6];
        var part = await CreatePartAsync(client, sku, mostrarLojaOnline: true);

        var del = await client.DeleteAsync($"/api/parts/{part.Id}");
        del.EnsureSuccessStatusCode();

        await AssertSingleDeliveryAsync(WebhookEvents.PartsRemovido, sku);
    }

    private async Task SubscribeAsync(HttpClient client, string @event)
    {
        var resp = await client.PostAsJsonAsync("/api/webhooks", new
        {
            Name = "Catalog sink " + @event,
            Url = "https://loja.test/" + @event,
            Events = new[] { @event },
        });
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<PartDto> CreatePartAsync(HttpClient client, string sku, bool mostrarLojaOnline)
    {
        var req = new CreatePartRequest(sku, "Peça " + sku, PartCategoria.Bateria,
            "Apple", "iPhone 12", null, 5, 1, 1500, null, null, null, mostrarLojaOnline);
        var resp = await client.PostAsJsonAsync("/api/parts", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PartDto>())!;
    }

    private async Task<WebhookDelivery> AssertSingleDeliveryAsync(string eventType, string skuMarker)
    {
        // Nota: a factory partilha-se entre testes, por isso podem existir várias subscriptions
        // activas para o mesmo evento (uma por teste). Cada publicação gera N deliveries.
        // Asseguramos apenas que ao menos UMA delivery apareceu para este SKU+evento — o SKU
        // é único por teste, portanto qualquer delivery filtrada pertence a este teste.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.WebhookDeliveries.IgnoreQueryFilters()
            .Where(d => d.EventType == eventType && d.PayloadJson.Contains(skuMarker))
            .ToListAsync();
        match.Should().NotBeEmpty($"esperava ao menos 1 delivery {eventType} para SKU {skuMarker}");
        return match[0];
    }

    private async Task AssertNoDeliveryAsync(string eventType, string skuMarker)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.WebhookDeliveries.IgnoreQueryFilters()
            .Where(d => d.EventType == eventType && d.PayloadJson.Contains(skuMarker))
            .CountAsync();
        count.Should().Be(0, $"não esperava deliveries {eventType} para SKU {skuMarker}");
    }

    private async Task<HttpClient> NewAdminClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<LoginResp>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed record LoginResp(string AccessToken, string RefreshToken);
}
