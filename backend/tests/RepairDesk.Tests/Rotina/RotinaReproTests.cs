using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Push;
using RepairDesk.Services.Rotina;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Rotina;

public class RotinaReproTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public RotinaReproTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RotinaDoBruno_QuartaAs1915_InsisteNoJantar()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Rotina", "rotina_bruno.json"));
        var resp = await client.PostAsync("/api/rotina/importar", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var n0 = await scope.ServiceProvider.GetRequiredService<IRotinaAvisosService>().ProcessarAsync(new DateTime(2026, 11, 11, 19, 0, 30, DateTimeKind.Utc));
        var n = await scope.ServiceProvider.GetRequiredService<IRotinaAvisosService>().ProcessarAsync(new DateTime(2026, 11, 11, 19, 15, 30, DateTimeKind.Utc));
        (n0, n).Should().Be((1, 1));
    }
}
