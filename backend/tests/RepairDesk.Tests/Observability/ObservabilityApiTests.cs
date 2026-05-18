using System.Net;
using System.Text.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Observability;

public class ObservabilityApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ObservabilityApiTests(RepairDeskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_EchoesCorrelationIdHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "test123");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().Should().Be("test123");
    }

    [Fact]
    public async Task HealthLive_GeneratesCorrelationId_WhenHeaderIsMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task HealthReady_ReturnsDependencyStatusJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("status").GetString().Should().Be("Healthy");

        var entries = root.GetProperty("entries");
        entries.TryGetProperty("db", out var db).Should().BeTrue();
        db.GetProperty("status").GetString().Should().Be("Healthy");
        entries.TryGetProperty("storage", out var storage).Should().BeTrue();
        storage.GetProperty("status").GetString().Should().Be("Healthy");
    }
}
