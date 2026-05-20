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
/// Sprint 130: parts.stock-baixo dispara só na transição above→below.
/// Movimento que mantém abaixo do mínimo não republica (evita spam).
/// </summary>
public class StockBaixoWebhookTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public StockBaixoWebhookTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Movimento_QueDescePartAbaixoDoMinimo_PublicaStockBaixo()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, "parts.stock-baixo");

        var sku = "SB-" + Guid.NewGuid().ToString("N")[..6];
        var part = await CreatePartAsync(client, sku, qtdStock: 5, qtdMinima: 2);

        // Movimento -4 → stock vai a 1, abaixo do minimo 2.
        var resp = await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-4, PartMovimentoMotivo.Saida, null, null));
        resp.EnsureSuccessStatusCode();

        await AssertDeliveryAsync("parts.stock-baixo", sku);
    }

    [Fact]
    public async Task Movimento_AcimaDoMinimo_NaoPublicaStockBaixo()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, "parts.stock-baixo");

        var sku = "OK-" + Guid.NewGuid().ToString("N")[..6];
        var part = await CreatePartAsync(client, sku, qtdStock: 10, qtdMinima: 2);

        var resp = await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-3, PartMovimentoMotivo.Saida, null, null));
        resp.EnsureSuccessStatusCode();

        await AssertNoDeliveryAsync("parts.stock-baixo", sku);
    }

    [Fact]
    public async Task SegundoMovimento_QueMantémAbaixo_NaoRepublica()
    {
        // Critério "transição": só dispara na primeira descida. Segundo movimento que
        // continua abaixo não deve gerar nova delivery.
        var client = await NewAdminClient();
        await SubscribeAsync(client, "parts.stock-baixo");

        var sku = "TR-" + Guid.NewGuid().ToString("N")[..6];
        var part = await CreatePartAsync(client, sku, qtdStock: 5, qtdMinima: 3);

        // 1.ª descida: 5 → 2 (atravessa o mínimo 3). Publica.
        (await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-3, PartMovimentoMotivo.Saida, null, null)))
            .EnsureSuccessStatusCode();

        // 2.º movimento: 2 → 1. Continua abaixo. Não publica (transição já feita).
        (await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-1, PartMovimentoMotivo.Saida, null, null)))
            .EnsureSuccessStatusCode();

        await AssertExactlyOneDeliveryAsync("parts.stock-baixo", sku);
    }

    [Fact]
    public async Task Create_ComStockJaAbaixo_PublicaImediatamente()
    {
        var client = await NewAdminClient();
        await SubscribeAsync(client, "parts.stock-baixo");

        var sku = "ZERO-" + Guid.NewGuid().ToString("N")[..6];
        await CreatePartAsync(client, sku, qtdStock: 0, qtdMinima: 2);

        await AssertDeliveryAsync("parts.stock-baixo", sku);
    }

    [Fact]
    public async Task Create_ComQtdMinima0_NuncaPublica()
    {
        // QtdMinima=0 desliga o alerta (tenant não quer ser incomodado com esta peça).
        var client = await NewAdminClient();
        await SubscribeAsync(client, "parts.stock-baixo");

        var sku = "NO-" + Guid.NewGuid().ToString("N")[..6];
        await CreatePartAsync(client, sku, qtdStock: 0, qtdMinima: 0);

        await AssertNoDeliveryAsync("parts.stock-baixo", sku);
    }

    private async Task SubscribeAsync(HttpClient client, string @event)
    {
        var resp = await client.PostAsJsonAsync("/api/webhooks", new
        {
            Name = "Stock sink " + @event,
            Url = "https://loja.test/" + @event,
            Events = new[] { @event },
        });
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<PartDto> CreatePartAsync(HttpClient client, string sku, int qtdStock, int qtdMinima)
    {
        var req = new CreatePartRequest(sku, "Peça " + sku, PartCategoria.Bateria,
            "Apple", "iPhone 12", null, qtdStock, qtdMinima, 1500, null, null, null, false);
        var resp = await client.PostAsJsonAsync("/api/parts", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PartDto>())!;
    }

    private async Task AssertDeliveryAsync(string eventType, string skuMarker)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.WebhookDeliveries.IgnoreQueryFilters()
            .Where(d => d.EventType == eventType && d.PayloadJson.Contains(skuMarker))
            .ToListAsync();
        match.Should().NotBeEmpty($"esperava delivery {eventType} para SKU {skuMarker}");
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

    private async Task AssertExactlyOneDeliveryAsync(string eventType, string skuMarker)
    {
        // Para este teste verificamos transição idempotente: 1 publish gera N deliveries
        // (N = nº de subscriptions). Como o teste cria 1 subscription, esperamos N=1.
        // Outros testes da suite podem ter criado mais subscriptions ao mesmo evento — esses
        // multiplicariam N. Para isolar, filtramos por SKU (único) e por subscription Name.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deliveriesPorSubscription = await db.WebhookDeliveries.IgnoreQueryFilters()
            .Where(d => d.EventType == eventType && d.PayloadJson.Contains(skuMarker))
            .GroupBy(d => d.WebhookSubscriptionId)
            .Select(g => new { SubId = g.Key, Count = g.Count() })
            .ToListAsync();
        // Cada subscription deve ter exactamente 1 delivery (não houve segunda publicação).
        deliveriesPorSubscription.Should().NotBeEmpty();
        deliveriesPorSubscription.All(g => g.Count == 1).Should().BeTrue(
            $"cada subscription devia ter 1 delivery (transição única), mas: {string.Join(", ", deliveriesPorSubscription.Select(g => $"sub={g.SubId} cnt={g.Count}"))}");
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
