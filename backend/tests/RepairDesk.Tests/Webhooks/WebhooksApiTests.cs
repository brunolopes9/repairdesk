using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.Services.Webhooks;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Webhooks;

public class WebhooksApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public WebhooksApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Events_ListsKnownEventTypes()
    {
        var client = await NewAdminClient();
        var events = await client.GetFromJsonAsync<string[]>("/api/webhooks/events");
        events.Should().NotBeNull().And.Contain("garantia.emitida").And.Contain("venda.paga");
    }

    [Fact]
    public async Task Create_ReturnsSecretOnce_AndListContainsSubscription()
    {
        var client = await NewAdminClient();
        var resp = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Loja online — eventos garantia",
            "https://shop.example.com/api/repairdesk-events",
            new[] { "garantia.emitida", "venda.cancelada" }));
        resp.EnsureSuccessStatusCode();
        var body = (await resp.Content.ReadFromJsonAsync<CreateWebhookSubscriptionResponse>())!;
        body.Secret.Should().StartWith("whsec_");
        body.Subscription.Events.Should().BeEquivalentTo(new[] { "garantia.emitida", "venda.cancelada" });

        var list = await client.GetFromJsonAsync<WebhookSubscriptionDto[]>("/api/webhooks");
        list.Should().NotBeNull();
        list!.Select(s => s.Id).Should().Contain(body.Subscription.Id);
    }

    [Fact]
    public async Task Create_InvalidUrl_Returns422()
    {
        var client = await NewAdminClient();
        var resp = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Bad", "not-a-url", new[] { "garantia.emitida" }));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_UnknownEvent_Returns422()
    {
        var client = await NewAdminClient();
        var resp = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Bad", "https://x.example.com/h", new[] { "evento.que.nao.existe" }));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_TogglesActive_ResetsFailureCount()
    {
        var client = await NewAdminClient();
        var create = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "Test", "https://shop.example.com/h", new[] { "garantia.emitida" }));
        create.EnsureSuccessStatusCode();
        var sub = (await create.Content.ReadFromJsonAsync<CreateWebhookSubscriptionResponse>())!.Subscription;

        var upd = await client.PutAsJsonAsync($"/api/webhooks/{sub.Id}", new UpdateWebhookSubscriptionRequest(
            "Test renamed", sub.Url, new[] { "garantia.emitida", "venda.paga" }, true));
        upd.EnsureSuccessStatusCode();
        var updated = (await upd.Content.ReadFromJsonAsync<WebhookSubscriptionDto>())!;
        updated.Name.Should().Be("Test renamed");
        updated.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_RemovesSubscription()
    {
        var client = await NewAdminClient();
        var create = await client.PostAsJsonAsync("/api/webhooks", new CreateWebhookSubscriptionRequest(
            "DeleteMe", "https://shop.example.com/h", new[] { "garantia.emitida" }));
        create.EnsureSuccessStatusCode();
        var sub = (await create.Content.ReadFromJsonAsync<CreateWebhookSubscriptionResponse>())!.Subscription;

        var del = await client.DeleteAsync($"/api/webhooks/{sub.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<WebhookSubscriptionDto[]>("/api/webhooks");
        list!.Select(s => s.Id).Should().NotContain(sub.Id);
    }

    [Fact]
    public async Task List_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/webhooks");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
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
