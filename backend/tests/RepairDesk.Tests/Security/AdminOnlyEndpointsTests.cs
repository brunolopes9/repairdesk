using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Security;

/// <summary>
/// Sprint 245 (Doc 72 Fase C): confirma que utilizadores autenticados sem role "Admin"
/// recebem 403 nos endpoints onde Sprints 243/244 adicionaram [Authorize(Roles="Admin")].
///
/// Pattern: o filtro [Authorize] corre antes da model validation, por isso payloads
/// vazios/inválidos chegam ao middleware authz e devolvem 403 independentemente do
/// corpo da request. Não precisamos de payloads válidos.
/// </summary>
public class AdminOnlyEndpointsTests : IClassFixture<RepairDeskApiFactory>
{
    private const string UserPassword = "User!Pass2026";
    private readonly RepairDeskApiFactory _factory;

    public AdminOnlyEndpointsTests(RepairDeskApiFactory factory) => _factory = factory;

    [Theory]
    // Sprint 243 Fase A — operações fiscais e estruturais P0
    [InlineData("DELETE", "/api/trabalhos/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/trabalhos/00000000-0000-0000-0000-000000000001/emitir-fatura")]
    [InlineData("POST", "/api/trabalhos/00000000-0000-0000-0000-000000000001/anular-fatura")]
    [InlineData("POST", "/api/trabalhos/bulk-emit-faturas")]
    [InlineData("POST", "/api/supplier-invoices/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/supplier-invoices/00000000-0000-0000-0000-000000000001/reject")]
    [InlineData("POST", "/api/supplier-invoices/00000000-0000-0000-0000-000000000001/reprocess")]
    [InlineData("POST", "/api/despesas")]
    [InlineData("DELETE", "/api/despesas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/parts/00000000-0000-0000-0000-000000000001/movimento")]
    [InlineData("POST", "/api/parts/import")]
    [InlineData("PUT", "/api/tenant-settings/me/preferences")]
    [InlineData("POST", "/api/llm-usage/anthropic-key")]
    [InlineData("DELETE", "/api/llm-usage/anthropic-key")]
    [InlineData("POST", "/api/automacoes/ingest-email/regenerate")]
    // Sprint 244 Fase B — configuração comercial/estrutural
    [InlineData("POST", "/api/price-table")]
    [InlineData("POST", "/api/price-table/import")]
    [InlineData("DELETE", "/api/price-table/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/diagnostico/templates")]
    [InlineData("DELETE", "/api/diagnostico/templates/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/clientes/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/clientes/import")]
    public async Task NonAdminUser_GetsForbidden_OnAdminOnlyEndpoint(string method, string path)
    {
        var user = await SeedUserAsync($"nonadmin-{Guid.NewGuid():N}@test.local");
        var client = NewClient();
        var auth = await Login(client, user.Email!, UserPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method is "POST" or "PUT" or "PATCH"
                ? JsonContent.Create(new { })
                : null,
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"{method} {path} deve recusar utilizadores sem role Admin");
    }

    [Fact]
    public async Task UnauthenticatedRequest_GetsUnauthorized_OnAdminEndpoint()
    {
        var client = NewClient();
        var response = await client.DeleteAsync("/api/despesas/00000000-0000-0000-0000-000000000001");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
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
            DisplayName = "Non-admin user",
            TenantId = RepairDeskApiFactory.TenantId,
            IsActive = true,
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
