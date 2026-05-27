using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Appointments;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Appointments;

/// <summary>Sprint 371: agendamentos — criar, listar por intervalo, mudar estado.</summary>
public class AppointmentApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AppointmentApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_List_UpdateStatus_Flow()
    {
        var client = await NewAuthedClient();
        var when = DateTime.UtcNow.AddDays(2).Date.AddHours(10);

        var create = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(
            null, "Maria Teste", "912345678", null, "iPhone 13", "Troca de ecrã", when, 45));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await create.Content.ReadFromJsonAsync<AppointmentDto>())!;
        dto.Nome.Should().Be("Maria Teste");
        dto.DurationMin.Should().Be(45);
        dto.Status.Should().Be("Agendado");

        var from = when.AddDays(-1).ToString("o");
        var to = when.AddDays(1).ToString("o");
        var list = await client.GetFromJsonAsync<List<AppointmentDto>>($"/api/appointments?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        list.Should().Contain(a => a.Id == dto.Id);

        var patch = await client.PatchAsJsonAsync($"/api/appointments/{dto.Id}/status", new UpdateAppointmentStatusRequest("Confirmado"));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await patch.Content.ReadFromJsonAsync<AppointmentDto>())!;
        updated.Status.Should().Be("Confirmado");
    }

    [Fact]
    public async Task List_SemAutenticacao_DaUnauthorized()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/appointments");
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
