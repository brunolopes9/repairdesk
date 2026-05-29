using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;

namespace RepairDesk.Tests.Auth;

/// <summary>
/// Sprint 427 (Doc 90 Tier 1 #2 follow-up): tests para PUT /api/auth/me (S420).
/// Foco nas regras: validação DisplayName/PhoneNumber length, auth obrigatória,
/// PhoneNumber opcional, refresh de UserInfo no payload de resposta.
/// </summary>
public class AuthUpdateMeApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public AuthUpdateMeApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UpdateMe_ValidPayload_ReturnsUpdatedUserInfo()
    {
        var client = await NewAuthedClient();

        var resp = await client.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest("Bruno Lopes Teste", "+351 912 345 678"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = (await resp.Content.ReadFromJsonAsync<UserInfo>())!;
        user.DisplayName.Should().Be("Bruno Lopes Teste");
        user.PhoneNumber.Should().Be("+351 912 345 678");
        user.Email.Should().Be(RepairDeskApiFactory.AdminEmail);
    }

    [Fact]
    public async Task UpdateMe_TrimsWhitespace()
    {
        var client = await NewAuthedClient();

        var resp = await client.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest("  Com Espaços  ", "  912000000  "));

        resp.EnsureSuccessStatusCode();
        var user = (await resp.Content.ReadFromJsonAsync<UserInfo>())!;
        user.DisplayName.Should().Be("Com Espaços");
        user.PhoneNumber.Should().Be("912000000");
    }

    [Fact]
    public async Task UpdateMe_NullPhone_ClearsExistingValue()
    {
        var client = await NewAuthedClient();
        // Primeiro setar um phone
        await client.PutAsJsonAsync("/api/auth/me", new UpdateMeRequest("Admin", "910000000"));

        var resp = await client.PutAsJsonAsync("/api/auth/me", new UpdateMeRequest("Admin", null));

        resp.EnsureSuccessStatusCode();
        var user = (await resp.Content.ReadFromJsonAsync<UserInfo>())!;
        user.PhoneNumber.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateMe_EmptyDisplayName_ReturnsBadRequest(string displayName)
    {
        var client = await NewAuthedClient();

        var resp = await client.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest(displayName, null));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMe_DisplayNameTooLong_ReturnsBadRequest()
    {
        var client = await NewAuthedClient();
        var tooLong = new string('a', 101);

        var resp = await client.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest(tooLong, null));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMe_PhoneTooLong_ReturnsBadRequest()
    {
        var client = await NewAuthedClient();
        var tooLong = new string('9', 31);

        var resp = await client.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest("Admin", tooLong));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMe_Anonymous_ReturnsUnauthorized()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.PutAsJsonAsync("/api/auth/me",
            new UpdateMeRequest("Hacker", null));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> NewAuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
