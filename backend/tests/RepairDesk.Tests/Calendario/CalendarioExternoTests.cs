using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Calendario;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Calendario;

/// <summary>
/// Sprint 619 (Doc 98 F4): calendários externos (iCal secreto do Google/Outlook/iCloud) vistos no Mender.
/// Repetições expandidas, só domínios conhecidos (SSRF), o endereço nunca volta na API, erro por calendário.
/// </summary>
public class CalendarioExternoTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Ics = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Google Inc//Google Calendar 70.9054//EN
        BEGIN:VEVENT
        UID:ginasio@google.com
        DTSTART:20310310T110000Z
        DTEND:20310310T123000Z
        RRULE:FREQ=WEEKLY;BYDAY=MO,WE
        SUMMARY:Ginásio
        LOCATION:Viseu
        END:VEVENT
        BEGIN:VEVENT
        UID:jantar@google.com
        DTSTART;VALUE=DATE:20310314
        DTEND;VALUE=DATE:20310315
        SUMMARY:Aniversário
        END:VEVENT
        BEGIN:VEVENT
        UID:cancelado@google.com
        DTSTART:20310311T090000Z
        DTEND:20310311T100000Z
        STATUS:CANCELLED
        SUMMARY:Cancelado
        END:VEVENT
        END:VCALENDAR
        """;

    private readonly RepairDeskApiFactory _factory;
    public CalendarioExternoTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public void Ics_ExpandeRepeticoes_IgnoraCancelados_EDiaInteiro()
    {
        var ev = CalendarioIcs.Eventos(Ics.Replace("\n", "\r\n"), new DateTime(2031, 3, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2031, 3, 17, 0, 0, 0, DateTimeKind.Utc));
        ev.Where(e => e.Titulo == "Ginásio").Select(e => e.InicioUtc.Day).Should().BeEquivalentTo([10, 12]);
        ev.Should().ContainSingle(e => e.Titulo == "Aniversário" && e.DiaInteiro);
        ev.Should().NotContain(e => e.Titulo == "Cancelado");
        ev.First(e => e.Titulo == "Ginásio").Local.Should().Be("Viseu");
    }

    [Theory]
    [InlineData("https://calendar.google.com/calendar/ical/x%40gmail.com/private-abc/basic.ics", true)]
    [InlineData("webcal://p12-caldav.icloud.com/published/2/abc", true)]
    [InlineData("http://calendar.google.com/x.ics", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    [InlineData("https://calendar.google.com.evil.com/x.ics", false)]
    [InlineData("https://calendar.google.com:8443/x.ics", false)]
    public void Url_SoDominiosConhecidos(string url, bool ok) => (CalendarioUrl.Normalizar(url) is not null).Should().Be(ok);

    [Fact]
    public async Task Ligar_NaoDevolveOEndereco_EMostraEventos()
    {
        var app = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IIcsFetcher>(new FakeFetcher(Ics))));
        var client = await Admin(app.CreateClient());
        const string secreto = "https://calendar.google.com/calendar/ical/bruno%40gmail.com/private-segredo123/basic.ics";

        (await client.PostAsJsonAsync("/api/calendarios-externos", new LigarCalendarioRequest("Google", "https://intranet.local/x.ics")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var resp = await client.PostAsJsonAsync("/api/calendarios-externos", new LigarCalendarioRequest("Google", secreto));
        resp.EnsureSuccessStatusCode();
        (await resp.Content.ReadAsStringAsync()).Should().NotContain("segredo123");
        (await client.GetStringAsync("/api/calendarios-externos")).Should().NotContain("segredo123").And.Contain("calendar.google.com");

        var eventos = await client.GetFromJsonAsync<EventosExternosDto>("/api/calendarios-externos/eventos?de=2031-03-10T00:00:00Z&ate=2031-03-17T00:00:00Z");
        eventos!.Eventos.Should().Contain(e => e.Titulo == "Ginásio" && e.Calendario == "Google");
        eventos.Calendarios.Should().ContainSingle(c => c.UltimoErro == null && c.UltimaLeitura != null);
    }

    private sealed class FakeFetcher(string ics) : IIcsFetcher
    {
        public Task<string> ObterAsync(Uri url, CancellationToken ct = default) => Task.FromResult(ics);
    }

    private static async Task<HttpClient> Admin(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
