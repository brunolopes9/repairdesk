using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Security;

/// <summary>
/// Sprint 249 (Doc 74): valida que SecurityHeadersMiddleware aplica os headers
/// esperados em respostas tanto autenticadas como anónimas.
/// </summary>
public class SecurityHeadersTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public SecurityHeadersTests(RepairDeskApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/health/live")]   // anónimo
    [InlineData("/api/auth/login")]    // anónimo (rejeita mas devolve resposta)
    public async Task AnonymousEndpoint_HasSecurityHeaders(string path)
    {
        var client = _factory.CreateClient();
        var response = path.Contains("login", StringComparison.Ordinal)
            ? await client.PostAsJsonAsync(path, new LoginRequest("x@y.z", "wrong"))
            : await client.GetAsync(path);

        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_HasSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task ResponseHasNoServerHeader()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/live");
        response.Headers.Contains("Server").Should().BeFalse();
        response.Headers.Contains("X-Powered-By").Should().BeFalse();
    }

    private static void AssertCommonHeaders(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
        nosniff!.Should().Contain("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var frame).Should().BeTrue();
        frame!.Should().Contain("DENY");

        response.Headers.TryGetValues("Referrer-Policy", out var referrer).Should().BeTrue();
        referrer!.First().Should().Contain("strict-origin-when-cross-origin");

        response.Headers.TryGetValues("Permissions-Policy", out var permissions).Should().BeTrue();
        permissions!.First().Should().Contain("camera=()");
        permissions!.First().Should().Contain("microphone=()");
        permissions!.First().Should().Contain("geolocation=()");

        response.Headers.TryGetValues("Cross-Origin-Opener-Policy", out var coop).Should().BeTrue();
        coop!.First().Should().Be("same-origin");

        response.Headers.TryGetValues("Cross-Origin-Resource-Policy", out var corp).Should().BeTrue();
        corp!.First().Should().Be("same-site");

        response.Headers.TryGetValues("Content-Security-Policy", out var csp).Should().BeTrue();
        csp!.First().Should().Contain("default-src 'none'");
        csp!.First().Should().Contain("frame-ancestors 'none'");
    }
}
