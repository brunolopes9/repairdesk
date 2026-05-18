using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.At;

public class AtNifLookupApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public AtNifLookupApiTests(RepairDeskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lookup_CacheMissCallsAt_ThenCacheHitAvoidsSecondCall()
    {
        var fake = new FakeAtNifRemoteClient();
        fake.Results["263758141"] = new AtNifLookupResult(
            "263758141",
            "Bruno Lopes (LopesTech)",
            "Viseu",
            "confirmed",
            DateTimeOffset.UtcNow);
        var client = await NewAuthedClient(fake);

        var first = await client.GetFromJsonAsync<AtNifLookupResult>("/api/at/nif-lookup/263758141");
        var second = await client.GetFromJsonAsync<AtNifLookupResult>("/api/at/nif-lookup/263758141");

        first!.Nome.Should().Be("Bruno Lopes (LopesTech)");
        second!.Nome.Should().Be("Bruno Lopes (LopesTech)");
        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Lookup_WhenAtReturnsNull_Returns404()
    {
        var fake = new FakeAtNifRemoteClient();
        var client = await NewAuthedClient(fake);

        var response = await client.GetAsync("/api/at/nif-lookup/123456789");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Lookup_WhenAtOffline_Returns503()
    {
        var fake = new FakeAtNifRemoteClient { ThrowUnavailable = true };
        var client = await NewAuthedClient(fake);

        var response = await client.GetAsync("/api/at/nif-lookup/263758141");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Lookup_RateLimitCountsCacheMissesPerTenant()
    {
        var fake = new FakeAtNifRemoteClient();
        fake.Results["263758141"] = new AtNifLookupResult("263758141", "Bruno Lopes", null, "confirmed", DateTimeOffset.UtcNow);
        fake.Results["123456789"] = new AtNifLookupResult("123456789", "Cliente Teste", null, "confirmed", DateTimeOffset.UtcNow);
        var client = await NewAuthedClient(fake, maxDailyCalls: 1);

        var first = await client.GetAsync("/api/at/nif-lookup/263758141");
        var second = await client.GetAsync("/api/at/nif-lookup/123456789");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be((HttpStatusCode)429);
        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Lookup_InvalidLocalNif_Returns422WithoutCallingAt()
    {
        var fake = new FakeAtNifRemoteClient();
        var client = await NewAuthedClient(fake);

        var response = await client.GetAsync("/api/at/nif-lookup/123456788");

        response.StatusCode.Should().Be((HttpStatusCode)422);
        fake.CallCount.Should().Be(0);
    }

    private async Task<HttpClient> NewAuthedClient(FakeAtNifRemoteClient fake, int maxDailyCalls = 100)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AtNifLookup:MaxDailyCallsPerTenant"] = maxDailyCalls.ToString(),
                    ["AtNifLookup:CacheTtlDays"] = "30",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAtNifRemoteClient>();
                services.AddSingleton<IAtNifRemoteClient>(fake);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class FakeAtNifRemoteClient : IAtNifRemoteClient
    {
        public Dictionary<string, AtNifLookupResult> Results { get; } = new();
        public bool ThrowUnavailable { get; init; }
        public int CallCount { get; private set; }

        public Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default)
        {
            CallCount++;
            if (ThrowUnavailable)
                throw new AtNifUnavailableException("AT offline in test.");

            Results.TryGetValue(nif, out var result);
            return Task.FromResult(result);
        }
    }
}
