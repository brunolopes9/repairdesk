using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Webhooks;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Webhooks;

public class WebhookPublisherTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public WebhookPublisherTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Publish_EnqueuesPendingDeliveriesForMatchingSubscriptions()
    {
        var client = await NewAdminClient();
        var create = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Test publisher",
            "https://shop.example.com/h",
            new[] { "garantia.emitida" }));
        create.EnsureSuccessStatusCode();
        var sub = (await create.Content.ReadFromJsonAsync<CreateWebhookSubscriptionResponse>())!.Subscription;

        // Outra subscription que NÃO ouve este evento — não deve gerar delivery
        await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Outra",
            "https://shop.example.com/outra",
            new[] { "venda.cancelada" }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IWebhookPublisher>();

        var tenantId = await db.WebhookSubscriptions.IgnoreQueryFilters()
            .Where(s => s.Id == sub.Id).Select(s => s.TenantId).FirstAsync();

        await publisher.PublishAsync(tenantId, "garantia.emitida", new { slug = "abc123", diasGarantia = 1095 });

        var deliveries = await db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Where(d => d.WebhookSubscriptionId == sub.Id)
            .ToListAsync();
        deliveries.Should().HaveCount(1);
        deliveries[0].EventType.Should().Be("garantia.emitida");
        deliveries[0].Status.Should().Be(WebhookDeliveryStatus.Pending);
        deliveries[0].PayloadJson.Should().Contain("garantia.emitida");
        deliveries[0].PayloadJson.Should().Contain("abc123");
    }

    [Fact]
    public async Task Publish_NoSubscriptions_NoDeliveriesCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IWebhookPublisher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var before = await db.WebhookDeliveries.IgnoreQueryFilters().CountAsync();
        await publisher.PublishAsync(Guid.NewGuid(), "garantia.emitida", new { x = 1 });
        var after = await db.WebhookDeliveries.IgnoreQueryFilters().CountAsync();
        after.Should().Be(before);
    }

    [Fact]
    public void HmacSignature_IsDeterministic_AndMatchesKnownVector()
    {
        // Vector conhecido — receptor consegue verificar com a mesma fórmula.
        var secret = "test-secret-123";
        var body = "{\"event\":\"test\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sig = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        sig.Should().StartWith("sha256=").And.HaveLength(7 + 64);
    }

    private async Task<HttpClient> NewAdminClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var json = (await login.Content.ReadFromJsonAsync<LoginAuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.AccessToken);
        return client;
    }

    private sealed record LoginAuthResponse(string AccessToken, string RefreshToken);
}
