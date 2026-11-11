using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Appointments;
using RepairDesk.Services.Push;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Appointments;

/// <summary>
/// Sprint 611: avisos da agenda — lembrete 1 h antes (uma vez; reagendar volta a avisar) e
/// resumos do dia (08:00) e da semana (segunda 08:00), que nunca saem duas vezes.
/// Datas em março de 2031 (sem hora de verão: Lisboa = UTC) para não colidir com outros testes.
/// </summary>
public class AgendaAvisosTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AgendaAvisosTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Lembrete_SaiUmaVez_EReagendarVoltaAAvisar()
    {
        var client = await Admin();
        var a = await Criar(client, "Leonardo Lembrete", "PS5", new DateTime(2031, 3, 12, 18, 0, 0, DateTimeKind.Utc));

        // Mais de 1 h antes: nada.
        (await Processar(new DateTime(2031, 3, 12, 16, 30, 0, DateTimeKind.Utc))).Lembretes.Should().Be(0);

        var r = await Processar(new DateTime(2031, 3, 12, 17, 15, 0, DateTimeKind.Utc));
        r.Lembretes.Should().Be(1);
        (await Jobs()).Should().Contain(j => j.Title.Contains("18:00 Leonardo Lembrete") && j.Title.Contains("daqui a 45 min") && j.Body.Contains("PS5"));

        // Segunda passagem: já avisado.
        (await Processar(new DateTime(2031, 3, 12, 17, 20, 0, DateTimeKind.Utc))).Lembretes.Should().Be(0);

        // Reagendar para as 19:00 → novo lembrete à nova hora.
        (await client.PatchAsJsonAsync($"/api/appointments/{a.Id}/reschedule",
            new RescheduleAppointmentRequest(new DateTime(2031, 3, 12, 19, 0, 0, DateTimeKind.Utc), null))).EnsureSuccessStatusCode();
        (await Processar(new DateTime(2031, 3, 12, 18, 30, 0, DateTimeKind.Utc))).Lembretes.Should().Be(1);
    }

    [Fact]
    public async Task ResumoDoDia_As8_SoUmaVez_ESemCancelados()
    {
        var client = await Admin();
        await Criar(client, "Ricardo Dia", "PS5", new DateTime(2031, 3, 13, 10, 0, 0, DateTimeKind.Utc));
        await Criar(client, "Ana Dia", "iPhone 13", new DateTime(2031, 3, 13, 15, 30, 0, DateTimeKind.Utc));
        var cancelado = await Criar(client, "Zé Cancelado", null, new DateTime(2031, 3, 13, 12, 0, 0, DateTimeKind.Utc));
        (await client.PatchAsJsonAsync($"/api/appointments/{cancelado.Id}/status", new UpdateAppointmentStatusRequest("Cancelado"))).EnsureSuccessStatusCode();
        await Jobs(); // limpa a fila

        (await Processar(new DateTime(2031, 3, 13, 7, 59, 0, DateTimeKind.Utc))).ResumosDia.Should().Be(0);

        var r = await Processar(new DateTime(2031, 3, 13, 8, 0, 30, DateTimeKind.Utc));
        r.ResumosDia.Should().Be(1);
        var resumo = (await Jobs()).Single(j => j.Tag == "dia:2031-03-13");
        resumo.Title.Should().Be("📅 Hoje tens 2 agendamentos");
        resumo.Body.Should().Contain("10:00 Ricardo Dia — PS5").And.Contain("15:30 Ana Dia — iPhone 13").And.NotContain("Zé");

        (await Processar(new DateTime(2031, 3, 13, 8, 5, 0, DateTimeKind.Utc))).ResumosDia.Should().Be(0);
    }

    [Fact]
    public async Task ResumoDaSemana_SoASegunda()
    {
        var client = await Admin();
        await Criar(client, "Semana Quarta", "PS5", new DateTime(2031, 3, 19, 10, 0, 0, DateTimeKind.Utc));
        await Jobs();

        // Domingo: não há resumo da semana.
        (await Processar(new DateTime(2031, 3, 16, 8, 1, 0, DateTimeKind.Utc))).ResumosSemana.Should().Be(0);

        var r = await Processar(new DateTime(2031, 3, 17, 8, 1, 0, DateTimeKind.Utc));
        r.ResumosSemana.Should().Be(1);
        (await Jobs()).Should().Contain(j => j.Tag == "semana:2031-W12" && j.Body.Contains("10:00 Semana Quarta"));
        (await Processar(new DateTime(2031, 3, 17, 8, 2, 0, DateTimeKind.Utc))).ResumosSemana.Should().Be(0);
    }

    private async Task<AgendaAvisosResultado> Processar(DateTime agoraUtc)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAgendaAvisosService>().ProcessarAsync(agoraUtc);
    }

    /// <summary>Esvazia a fila de push (nos testes não há worker a consumi-la).</summary>
    private async Task<List<StaffPushJob>> Jobs()
    {
        var fila = _factory.Services.GetRequiredService<IStaffPushQueue>();
        var jobs = new List<StaffPushJob>();
        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            try { jobs.Add(await fila.DequeueAsync(cts.Token)); }
            catch (OperationCanceledException) { return jobs; }
        }
    }

    private static async Task<AppointmentDto> Criar(HttpClient client, string nome, string? equipamento, DateTime quando)
    {
        var resp = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(null, nome, null, null, equipamento, null, quando, 60));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AppointmentDto>())!;
    }

    private async Task<HttpClient> Admin()
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
