using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;

namespace RepairDesk.Tests.Auth;

public class TenantIsolationApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public TenantIsolationApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TwoUsersInDifferentTenants_GetTheirOwnTenantIdInJwt()
    {
        var clientA = NewClient();
        var clientB = NewClient();

        var authA = await Login(clientA, RepairDeskApiFactory.AdminEmail);
        var authB = await Login(clientB, RepairDeskApiFactory.SecondAdminEmail);

        authA.User.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
        authB.User.TenantId.Should().Be(RepairDeskApiFactory.SecondTenantId);
        authA.User.TenantId.Should().NotBe(authB.User.TenantId);

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);

        var meA = await clientA.GetFromJsonAsync<UserInfo>("/api/auth/me");
        var meB = await clientB.GetFromJsonAsync<UserInfo>("/api/auth/me");

        meA!.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
        meB!.TenantId.Should().Be(RepairDeskApiFactory.SecondTenantId);
        meA.Id.Should().NotBe(meB.Id);
    }

    [Fact]
    public async Task TenantA_Bearer_CannotResolveTenantB_User_OnMe()
    {
        // /me only returns the authenticated user. Each tenant's user resolution must go through
        // the AppDbContext global filter, so a JWT for tenantA cannot expose tenantB's user.
        var client = NewClient();
        var authA = await Login(client, RepairDeskApiFactory.AdminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var me = await client.GetFromJsonAsync<UserInfo>("/api/auth/me");
        me!.Email.Should().Be(RepairDeskApiFactory.AdminEmail);
        me.TenantId.Should().Be(RepairDeskApiFactory.TenantId);
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private static async Task<AuthResponse> Login(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
