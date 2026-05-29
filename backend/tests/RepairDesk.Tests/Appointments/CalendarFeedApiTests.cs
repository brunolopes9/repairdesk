using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Appointments;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Appointments;

/// <summary>
/// Sprint 447: cobre o calendar-feed token (S443) + inclusão de reparações com ETA (S446).
/// Focado em: lazy-gen do token, rotação invalida o anterior, public feed valida token,
/// .ics contém appointments + reparações com PrevistoEntregueEm.
/// </summary>
public class CalendarFeedApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public CalendarFeedApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record FeedInfo(string Token, string PublicPath);

    [Fact]
    public async Task Get_PrimeiraChamada_GeraToken()
    {
        var admin = await NewAuthedClient();
        var resp = await admin.GetAsync("/api/automacoes/calendar-feed");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var feed = (await resp.Content.ReadFromJsonAsync<FeedInfo>())!;
        feed.Token.Should().NotBeNullOrWhiteSpace();
        feed.Token.Length.Should().BeGreaterThanOrEqualTo(16);
        feed.PublicPath.Should().StartWith("/api/public/calendar-feed/").And.EndWith(".ics");

        // Segunda chamada — devolve o mesmo token (não rotaciona em GETs).
        var again = (await admin.GetFromJsonAsync<FeedInfo>("/api/automacoes/calendar-feed"))!;
        again.Token.Should().Be(feed.Token);
    }

    [Fact]
    public async Task Regenerate_Rotaciona_TokenAnteriorInvalida()
    {
        var admin = await NewAuthedClient();
        var initial = (await admin.GetFromJsonAsync<FeedInfo>("/api/automacoes/calendar-feed"))!;

        var rotated = await admin.PostAsync("/api/automacoes/calendar-feed/regenerate", null);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = (await rotated.Content.ReadFromJsonAsync<FeedInfo>())!;
        next.Token.Should().NotBe(initial.Token);

        // Token anterior deixou de ser válido — 404 no public endpoint.
        var anon = _factory.CreateClient();
        var oldIcs = await anon.GetAsync($"/api/public/calendar-feed/{initial.Token}.ics");
        oldIcs.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Token novo funciona.
        var newIcs = await anon.GetAsync($"/api/public/calendar-feed/{next.Token}.ics");
        newIcs.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicFeed_TokenInvalido_404()
    {
        var anon = _factory.CreateClient();
        // Token com formato válido (hex 32 chars) mas inexistente — 404.
        var resp = await anon.GetAsync($"/api/public/calendar-feed/{Guid.NewGuid():N}.ics");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicFeed_TokenComCaracteresInvalidos_404()
    {
        var anon = _factory.CreateClient();
        // Anti-injecção: tudo o que não seja [a-zA-Z0-9] é rejeitado antes de tocar na BD.
        var resp = await anon.GetAsync("/api/public/calendar-feed/abc;DROP%20TABLE.ics");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicFeed_IncluiAgendamentoCriado()
    {
        var admin = await NewAuthedClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var when = DateTime.UtcNow.AddDays(3).Date.AddHours(11);

        // Cria agendamento.
        var create = await admin.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(
            null, $"FeedTest {marker}", "912000000", null, $"iPhone {marker}", "Bateria", when, 30));
        create.EnsureSuccessStatusCode();

        // Buscar token + ler .ics público.
        var feed = (await admin.GetFromJsonAsync<FeedInfo>("/api/automacoes/calendar-feed"))!;
        var anon = _factory.CreateClient();
        var ics = await anon.GetAsync($"/api/public/calendar-feed/{feed.Token}.ics?days=30");
        ics.StatusCode.Should().Be(HttpStatusCode.OK);
        ics.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");

        var body = await ics.Content.ReadAsStringAsync();
        body.Should().Contain("BEGIN:VCALENDAR");
        body.Should().Contain($"FeedTest {marker}");
        body.Should().Contain($"iPhone {marker}");
    }

    [Fact]
    public async Task PublicFeed_IncluiReparacaoComEta_Sprint446()
    {
        var admin = await NewAuthedClient();
        var marker = Guid.NewGuid().ToString("N")[..8];

        // Cria cliente.
        var cliente = await admin.PostAsJsonAsync("/api/clientes", new
        {
            Nome = $"Cliente Feed {marker}",
            Telefone = "913000000",
            Email = (string?)null,
        });
        cliente.EnsureSuccessStatusCode();
        var clienteDto = await cliente.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var clienteId = clienteDto!["id"].ToString()!;

        // Cria reparação.
        var rep = await admin.PostAsJsonAsync("/api/reparacoes", new
        {
            ClienteId = clienteId,
            Equipamento = $"Samsung A52 {marker}",
            Avaria = "Não liga",
        });
        rep.EnsureSuccessStatusCode();
        var repDto = await rep.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var repId = repDto!["id"].ToString()!;

        // Define ETA via PUT genérico /api/reparacoes/{id} (campo PrevistoEntregueEm
        // foi adicionado em S419). O update overwrites todos os campos — copiamos
        // os mínimos obrigatórios da reparação criada.
        var eta = DateTime.UtcNow.AddDays(2).Date.AddHours(14);
        var etaResp = await admin.PutAsJsonAsync($"/api/reparacoes/{repId}", new
        {
            Equipamento = $"Samsung A52 {marker}",
            Avaria = "Não liga",
            Imei = (string?)null,
            Diagnostico = (string?)null,
            OrcamentoCents = (int?)null,
            OrcamentoAprovado = false,
            PrecoFinalCents = (int?)null,
            CustoPecasCents = 0,
            HorasGastas = 0m,
            Notas = (string?)null,
            EstadoPagamento = 0,
            ClienteId = clienteId,
            PrevistoEntregueEm = eta,
        });
        etaResp.EnsureSuccessStatusCode();

        // Buscar feed.
        var feed = (await admin.GetFromJsonAsync<FeedInfo>("/api/automacoes/calendar-feed"))!;
        var anon = _factory.CreateClient();
        var ics = await anon.GetAsync($"/api/public/calendar-feed/{feed.Token}.ics?days=30");
        ics.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ics.Content.ReadAsStringAsync();
        // Reparação aparece com prefix "rep-" no UID + equipamento na SUMMARY.
        body.Should().Contain($"rep-{repId}@mender");
        body.Should().Contain($"Samsung A52 {marker}");
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
