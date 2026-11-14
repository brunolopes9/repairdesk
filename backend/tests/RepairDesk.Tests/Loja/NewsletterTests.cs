using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Loja;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Loja;

/// <summary>Sprint 661: newsletter da loja — confirmação dupla, cancelamento, bots e lista do Admin.</summary>
public class NewsletterTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;
    private readonly EmailsCapturados _emails = new();

    public NewsletterTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(_emails);
        }));
    }

    [Fact]
    public async Task Subscrever_ConfirmarPeloEmail_Cancelar()
    {
        var admin = await Admin();
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();
        var email = $"news{Guid.NewGuid():N}@example.com";

        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter", new SubscreverNewsletterRequest(email.ToUpperInvariant(), "loja", true)))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var msg = _emails.Para(email).Should().ContainSingle().Subject;
        var confirmar = Token(msg.Text, "confirmar");
        var cancelar = Token(msg.Text, "cancelar");

        // Antes de confirmar não conta como subscrito.
        (await Estado(admin, email)).Should().Be("por_confirmar");

        // Pedir outra vez logo a seguir não manda segundo email (anti-spam).
        await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter", new SubscreverNewsletterRequest(email, "loja", true));
        _emails.Para(email).Should().HaveCount(1);

        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter/confirmar", new TokenNewsletterRequest(confirmar)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Estado(admin, email)).Should().Be("ativo");

        // O link de confirmação só serve uma vez.
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter/confirmar", new TokenNewsletterRequest(confirmar))).StatusCode)
            .Should().BeOneOf(400, 422);

        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter/cancelar", new TokenNewsletterRequest(cancelar)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Estado(admin, email)).Should().Be("cancelado");
        // Cancelar outra vez (ou com token desconhecido) também responde OK.
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter/cancelar", new TokenNewsletterRequest("nao-existe")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SemConsentimento_EmailInvalido_EBot_NaoGuardamNada()
    {
        var admin = await Admin();
        var slug = await IntakeSlug(admin);
        var anon = _app.CreateClient();
        var email = $"bot{Guid.NewGuid():N}@example.com";

        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter", new SubscreverNewsletterRequest(email, null, false))).StatusCode)
            .Should().BeOneOf(400, 422);
        ((int)(await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter", new SubscreverNewsletterRequest("nao-e-email", null, true))).StatusCode)
            .Should().BeOneOf(400, 422);
        (await anon.PostAsJsonAsync($"/api/public/loja/{slug}/newsletter", new SubscreverNewsletterRequest(email, null, true, Website: "http://spam")))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        _emails.Para(email).Should().BeEmpty();
        (await Estado(admin, email)).Should().BeNull();
    }

    [Fact]
    public async Task ListaDoAdmin_ExigeLogin()
        => (await _app.CreateClient().GetAsync("/api/loja/newsletter")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    // ---------- helpers ----------

    private static string Token(string texto, string parametro)
        => Uri.UnescapeDataString(Regex.Match(texto, $@"[?&]{parametro}=([A-Za-z0-9_%\-]+)").Groups[1].Value);

    private static async Task<string?> Estado(HttpClient admin, string email)
    {
        var r = (await admin.GetFromJsonAsync<NewsletterResumoDto>("/api/loja/newsletter"))!;
        return r.Subscritores.FirstOrDefault(s => s.Email == email.ToLowerInvariant())?.Estado;
    }

    private static async Task<string> IntakeSlug(HttpClient admin)
    {
        var resp = await admin.GetAsync("/api/automacoes/intake-widget");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IntakeWidgetDto>())!.Slug;
    }

    private sealed record IntakeWidgetDto(string Slug, string? PublicUrl);

    private async Task<HttpClient> Admin()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class EmailsCapturados : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _enviados = new();
        public bool IsConfigured => true;
        public IReadOnlyList<EmailMessage> Para(string email) => _enviados.Where(m => m.To == email.ToLowerInvariant()).ToList();
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { _enviados.Enqueue(message); return Task.CompletedTask; }
    }
}
