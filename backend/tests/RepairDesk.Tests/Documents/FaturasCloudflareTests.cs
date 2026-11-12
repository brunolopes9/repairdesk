using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MimeKit;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Documents;

/// <summary>Sprint 632: o Email Worker da Cloudflare envia o email inteiro; só entra com o segredo certo e o endereço da empresa.</summary>
public class FaturasCloudflareTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FaturasCloudflareTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Mime_SoComSegredo_ESemPdfPedeReencaminhamento()
    {
        var admin = await Admin();
        var gerado = await (await admin.PostAsJsonAsync("/api/faturas-email/cloudflare", new EnderecoCloudflareRequest("Faturas@LopesTech.pt")))
            .Content.ReadFromJsonAsync<SegredoGeradoDto>();
        gerado!.Endereco.Should().Be("faturas@lopestech.pt");
        (await admin.GetStringAsync("/api/faturas-email")).Should().NotContain(gerado.Segredo).And.Contain("\"temSegredo\":true");

        var anon = _factory.CreateClient();
        async Task<HttpResponseMessage> Enviar(string segredo, string para)
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse("forwarding-noreply@google.com"));
            msg.To.Add(MailboxAddress.Parse(para));
            msg.Subject = "Confirmação de reencaminhamento do Gmail";
            msg.Body = new TextPart("plain") { Text = "Código: 123456" };
            using var ms = new MemoryStream();
            await msg.WriteToAsync(ms);
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/external/email-ingest/mime") { Content = new ByteArrayContent(ms.ToArray()) };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
            req.Headers.Add("X-Para", para);
            req.Headers.Add("X-Webhook-Secret", segredo);
            return await anon.SendAsync(req);
        }

        (await Enviar("errado", "faturas@lopestech.pt")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Enviar(gerado.Segredo, "outro@lopestech.pt")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var ok = await Enviar(gerado.Segredo, "faturas@lopestech.pt");
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        (await ok.Content.ReadAsStringAsync()).Should().Contain("\"semPdf\":true");
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
