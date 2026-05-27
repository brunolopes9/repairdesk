using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RepairDesk.API.Assistant;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Assistant;

/// <summary>
/// Sprint 369: assistente interno. Verifica wiring + auth + degradação graciosa sem chave de
/// IA (sem rede). A garantia read-only é estrutural: AssistantService só tem 3 tools de SELECT
/// (AsNoTracking) e zero chamadas de escrita — não há tool que crie/altere/apague.
/// </summary>
public class AssistantApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AssistantApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> NoKeyFactory() => _factory.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "" }));
    });

    [Fact]
    public async Task Ask_SemAutenticacao_DaUnauthorized()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest("quanto stock?", null));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ask_SemChaveIA_RespondeGraciosamenteSemRede()
    {
        var factory = NoKeyFactory();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PostAsJsonAsync("/api/assistant/ask",
            new AssistantAskRequest("Quanto stock há de ecrãs Samsung?", null));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = await resp.Content.ReadFromJsonAsync<AssistantAnswer>();
        answer!.Answer.Should().Contain("não está configurado");
    }
}
