using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Push;

/// <summary>
/// Sprint 366: subscrição de push de STAFF + envio para todos os dispositivos do tenant
/// (gatilho: pedido online novo). Sem rede — IWebPushSender substituído por um gravador.
/// </summary>
public class StaffPushApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public StaffPushApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Subscribe_GuardaComUserIdETenant_DepoisNotifyEnviaParaOEndpoint()
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

        var admin = await NewAuthedClient(factory);
        var sub = NewSubscription();

        var subscribe = await admin.PostAsJsonAsync("/api/push/subscribe", sub);
        subscribe.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.StaffPushSubscriptions.IgnoreQueryFilters()
                .SingleAsync(x => x.Endpoint == sub.Endpoint);
            saved.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
            saved.UserId.Should().NotBeEmpty();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var staff = scope.ServiceProvider.GetRequiredService<IStaffPushService>();
            var sent = await staff.NotifyTenantAsync(new StaffPushJob(
                RepairDeskApiFactory.TenantId, "Novo pedido online", "Maria — iPhone 13", "/pedidos-online", "repair-request"));
            sent.Should().Be(1);
        }

        sender.Sent.Should().ContainSingle();
        sender.Sent[0].Payload.Should().Contain("Novo pedido online");
        sender.Sent[0].Payload.Should().Contain("/pedidos-online");
    }

    [Fact]
    public async Task Subscribe_SemAutenticacao_DaUnauthorized()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync("/api/push/subscribe", NewSubscription());
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private static BrowserPushSubscriptionDto NewSubscription()
        => new(
            $"https://updates.push.services.mozilla.com/wpush/v2/{Guid.NewGuid():N}",
            null,
            new BrowserPushKeysDto("BOr6NQh6Z6u4o5w2s9XkpNfFakeP256dhKeyForTests", "fakeAuthKeyForTests"));

    private static async Task<HttpClient> NewAuthedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
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
