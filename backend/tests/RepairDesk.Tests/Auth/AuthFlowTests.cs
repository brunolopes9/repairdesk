using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;

namespace RepairDesk.Tests.Auth;

public class AuthFlowTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public AuthFlowTests(RepairDeskApiFactory factory) => _factory = factory;

    private static HttpClient NewClient(RepairDeskApiFactory f) =>
        f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_AndIssuesTokens()
    {
        var client = NewClient(_factory);

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be(RepairDeskApiFactory.AdminEmail);
        body.User.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
        body.User.Roles.Should().Contain("Admin");
        resp.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("rd_refresh") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_InvalidCredentials()
    {
        var client = NewClient(_factory);

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, "Wrong!Pass2026"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        body!.Code.Should().BeOneOf("invalid_credentials", "locked_out");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401_InvalidCredentials()
    {
        var client = NewClient(_factory);

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nope@nowhere.test", "Whatever!1"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        body!.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = NewClient(_factory);
        var resp = await client.GetAsync("/api/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithBearer_ReturnsUserInfo_WithExpectedTenant()
    {
        var client = NewClient(_factory);
        var auth = await Login(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var resp = await client.GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await resp.Content.ReadFromJsonAsync<UserInfo>();
        me!.Email.Should().Be(RepairDeskApiFactory.AdminEmail);
        me.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
        me.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = NewClient(_factory);
        var resp = await client.PostAsync("/api/auth/refresh", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesAndIssuesNewAccessToken()
    {
        var client = NewClient(_factory);
        var first = await Login(client);

        var resp = await client.PostAsync("/api/auth/refresh", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        second!.AccessToken.Should().NotBe(first.AccessToken);
        second.User.Id.Should().Be(first.User.Id);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_FurtherRefreshFails()
    {
        var client = NewClient(_factory);
        var auth = await Login(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var logout = await client.PostAsync("/api/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await client.PostAsync("/api/auth/refresh", content: null);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<AuthResponse> Login(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record ErrorBody(string Code);
}
