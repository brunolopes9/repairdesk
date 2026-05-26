using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Push;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.PublicPortal;

public class PushNotificationsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public PushNotificationsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task VapidPublicKey_GeneratesAndPersistsKeys()
    {
        var client = _factory.CreateClient();

        var key = await client.GetFromJsonAsync<VapidPublicKeyDto>("/api/public/portal/push/vapid-public-key");

        key!.PublicKey.Should().NotBeNullOrWhiteSpace();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.SystemSettings.CountAsync(x => x.Key.StartsWith("Push:Vapid"))).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Subscribe_And_Unsubscribe_StoresOnlyEndpointAndKeys()
    {
        var admin = await NewAuthedClient(_factory, RepairDeskApiFactory.AdminEmail);
        var reparacao = await CreateRepair(admin);
        var publicClient = _factory.CreateClient();

        var subscription = NewSubscription();
        var subResponse = await publicClient.PostAsJsonAsync($"/api/public/portal/{reparacao.PublicSlug}/push/subscribe", subscription);
        subResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.PushSubscriptions.SingleAsync(x => x.ReparacaoId == reparacao.Id);
            saved.Endpoint.Should().Be(subscription.Endpoint);
            saved.P256dh.Should().Be(subscription.Keys.P256dh);
            saved.Auth.Should().Be(subscription.Keys.Auth);
            saved.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
        }

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/public/portal/{reparacao.PublicSlug}/push/unsubscribe")
        {
            Content = JsonContent.Create(new UnsubscribePushRequest(subscription.Endpoint))
        };
        var unsubResponse = await publicClient.SendAsync(delete);
        unsubResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.PushSubscriptions.CountAsync(x => x.ReparacaoId == reparacao.Id)).Should().Be(0);
        }
    }

    [Fact]
    public async Task SendRepairStatusChanged_UsesSubscribedEndpoint()
    {
        var sender = new RecordingWebPushSender();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWebPushSender>();
                services.AddSingleton<IWebPushSender>(sender);
            });
        });

        var admin = await NewAuthedClient(factory, RepairDeskApiFactory.AdminEmail);
        var reparacao = await CreateRepair(admin);
        var publicClient = factory.CreateClient();
        var subscribe = await publicClient.PostAsJsonAsync($"/api/public/portal/{reparacao.PublicSlug}/push/subscribe", NewSubscription());
        subscribe.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var sent = await push.SendRepairStatusChangedAsync(reparacao.Id);

        sent.Should().Be(1);
        sender.Sent.Should().ContainSingle();
        sender.Sent[0].Payload.Should().Contain($"/r/{reparacao.PublicSlug}");
        sender.Sent[0].Payload.Should().Contain("iPhone 12");
    }

    private static BrowserPushSubscriptionDto NewSubscription()
        => new(
            $"https://updates.push.services.mozilla.com/wpush/v2/{Guid.NewGuid():N}",
            null,
            new BrowserPushKeysDto(
                "BOr6NQh6Z6u4o5w2s9XkpNfFakeP256dhKeyForTests",
                "fakeAuthKeyForTests"));

    private static async Task<HttpClient> NewAuthedClient(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ReparacaoDto> CreateRepair(HttpClient client)
    {
        var phone = "9" + Random.Shared.Next(10000000, 99999999);
        var clienteResp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest($"Cliente Push {Guid.NewGuid():N}", phone, null, null, null));
        clienteResp.EnsureSuccessStatusCode();
        var cliente = (await clienteResp.Content.ReadFromJsonAsync<ClienteDto>())!;

        var repResp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(cliente.Id, "iPhone 12", "Ecrã partido", null, 8900, null, RepairStatus.Recebido));
        repResp.EnsureSuccessStatusCode();
        return (await repResp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }

    private sealed class RecordingWebPushSender : IWebPushSender
    {
        public List<(WebPushTarget Target, string Payload)> Sent { get; } = new();

        public Task SendAsync(WebPushTarget target, string payload, VapidKeys keys, CancellationToken ct = default)
        {
            Sent.Add((target, payload));
            return Task.CompletedTask;
        }
    }
}
