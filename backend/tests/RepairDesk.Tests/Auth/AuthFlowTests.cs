using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

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

    [Fact]
    public async Task ChangePassword_ClearsRequiredPasswordChangeFlag_AndAllowsNewPassword()
    {
        var email = $"seed-{Guid.NewGuid():N}@test.local";
        const string oldPassword = "Temp!Pass2026";
        const string newPassword = "New!Pass2026";
        await SeedPasswordChangeUser(email, oldPassword);

        var client = NewClient(_factory);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, oldPassword));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        auth.User.RequireChangePasswordOnNextLogin.Should().BeTrue();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(oldPassword, newPassword));

        change.StatusCode.Should().Be(HttpStatusCode.OK);
        var changed = (await change.Content.ReadFromJsonAsync<AuthResponse>())!;
        changed.User.RequireChangePasswordOnNextLogin.Should().BeFalse();

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, oldPassword));
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword));
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<AuthResponse> Login(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task SeedPasswordChangeUser(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = db.Roles.Single(r => r.Name == "Admin");
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Seed Password User",
            TenantId = RepairDeskApiFactory.TenantId,
            IsActive = true,
            RequireChangePasswordOnNextLogin = true
        };

        var result = await users.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Code)));
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
    }

    private sealed record ErrorBody(string Code);
}
