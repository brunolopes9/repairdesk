using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Reparacoes;

/// <summary>
/// Sprint 349/352: time-tracker. Cobre o invariante "um timer activo por user"
/// e o auto-stop ao mudar a reparação para estado terminal (Sprint 352).
/// </summary>
public class TimeEntriesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public TimeEntriesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record ActiveTimerDto(Guid Id, Guid ReparacaoId, int ReparacaoNumero, DateTime StartedAt);
    private sealed record TimeEntryDto(Guid Id, Guid ReparacaoId, Guid UserId, DateTime StartedAt, DateTime? EndedAt, int? DuracaoMinutos, string? Notas);
    private sealed record StartReq(Guid ReparacaoId, string? Notas);
    private sealed record ChangeEstadoReq(int Estado, string? Notas);

    [Fact]
    public async Task Start_SegundoStart_FechaTimerAnterior()
    {
        var client = await NewAuthedClient();
        var cliente = await CreateClienteAsync(client);
        var rep1 = await CreateReparacaoAsync(client, cliente.Id);
        var rep2 = await CreateReparacaoAsync(client, cliente.Id);

        var start1 = await client.PostAsJsonAsync("/api/time-entries/start", new StartReq(rep1.Id, null));
        start1.EnsureSuccessStatusCode();
        var entry1 = (await start1.Content.ReadFromJsonAsync<TimeEntryDto>())!;

        // Segundo start noutra reparação fecha o primeiro.
        var start2 = await client.PostAsJsonAsync("/api/time-entries/start", new StartReq(rep2.Id, null));
        start2.EnsureSuccessStatusCode();

        var active = await client.GetFromJsonAsync<ActiveTimerDto?>("/api/time-entries/active");
        active!.ReparacaoId.Should().Be(rep2.Id, "só pode haver um timer activo por user");

        var entriesRep1 = await client.GetFromJsonAsync<List<TimeEntryDto>>($"/api/time-entries/by-reparacao/{rep1.Id}");
        entriesRep1!.Single(e => e.Id == entry1.Id).EndedAt.Should().NotBeNull("o timer anterior deve ter sido fechado");
    }

    [Fact]
    public async Task ChangeEstado_Entregue_FechaTimerActivo()
    {
        var client = await NewAuthedClient();
        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);

        await client.PostAsJsonAsync("/api/time-entries/start", new StartReq(rep.Id, "a trabalhar"));

        // Recebido(0) → Diagnostico(1) → Reparado(4) → Entregue(5).
        await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoReq(1, null));
        await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoReq(4, null));
        var entregar = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoReq(5, null));
        entregar.EnsureSuccessStatusCode();

        // Sprint 352: ao Entregar, o timer activo é fechado automaticamente.
        // Sprint 358: sem timer activo, o endpoint devolve 204 NoContent.
        var activeResp = await client.GetAsync("/api/time-entries/active");
        activeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await client.GetFromJsonAsync<List<TimeEntryDto>>($"/api/time-entries/by-reparacao/{rep.Id}");
        entries!.Should().OnlyContain(e => e.EndedAt != null);
    }

    [Fact]
    public async Task Stop_TimerJaParado_409()
    {
        var client = await NewAuthedClient();
        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);

        var start = await client.PostAsJsonAsync("/api/time-entries/start", new StartReq(rep.Id, null));
        var entry = (await start.Content.ReadFromJsonAsync<TimeEntryDto>())!;

        var stop1 = await client.PostAsync($"/api/time-entries/{entry.Id}/stop", null);
        stop1.EnsureSuccessStatusCode();

        var stop2 = await client.PostAsync($"/api/time-entries/{entry.Id}/stop", null);
        stop2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> NewAuthedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Timer " + Guid.NewGuid().ToString("N")[..6], "912000222", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client, Guid clienteId)
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, "iPhone 12", "Não carrega", null, 8000, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }
}
