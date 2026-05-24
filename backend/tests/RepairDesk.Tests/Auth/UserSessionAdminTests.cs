using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Auth;

public class UserSessionAdminTests : IClassFixture<RepairDeskApiFactory>
{
    private const string UserPassword = "User!Pass2026";
    private readonly RepairDeskApiFactory _factory;

    public UserSessionAdminTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_CanRevokeAllSessions_ForUser()
    {
        var target = await SeedUserAsync($"revoke-{Guid.NewGuid():N}@test.local");
        var targetClient = NewClient();
        await Login(targetClient, target.Email!, UserPassword);

        var admin = NewClient();
        var adminAuth = await Login(admin, RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        var response = await admin.PostAsync($"/api/users/{target.Id}/revoke-sessions", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<RevokeUserSessionsResponse>())!;
        body.RevokedCount.Should().BeGreaterThan(0);

        var refresh = await targetClient.PostAsync("/api/auth/refresh", content: null);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonAdmin_CannotRevokeUserSessions()
    {
        var user = await SeedUserAsync($"nonadmin-{Guid.NewGuid():N}@test.local");
        var client = NewClient();
        var auth = await Login(client, user.Email!, UserPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsync($"/api/users/{user.Id}/revoke-sessions", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateUser_RevokesTokens_AndBlocksRefresh()
    {
        var target = await SeedUserAsync($"deactivate-{Guid.NewGuid():N}@test.local");
        var targetClient = NewClient();
        await Login(targetClient, target.Email!, UserPassword);

        var admin = NewClient();
        var adminAuth = await Login(admin, RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        var response = await admin.PostAsJsonAsync(
            $"/api/users/{target.Id}/deactivate",
            new DeactivateUserRequest("suspeita de conta comprometida"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await targetClient.PostAsync("/api/auth/refresh", content: null);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedUser = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == target.Id);
        updatedUser.IsActive.Should().BeFalse();

        var audit = await db.AuditEntries
            .IgnoreQueryFilters()
            .Where(x => x.Action == AuditAction.UserDeactivated && x.EntityId == target.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private async Task<AppUser> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "User Sessions Test",
            TenantId = RepairDeskApiFactory.TenantId,
            IsActive = true
        };

        var result = await users.CreateAsync(user, UserPassword);
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Code)));
        return user;
    }

    private static async Task<AuthResponse> Login(HttpClient client, string email, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
