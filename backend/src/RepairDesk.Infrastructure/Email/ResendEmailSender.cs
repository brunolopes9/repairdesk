using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.Email;

/// <summary>
/// Envia emails pela API HTTP do Resend (https://resend.com/docs/api-reference/emails/send-email).
/// Sem API key configurada não envia nada e regista um aviso — nunca rebenta o pedido do utilizador.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private const string Endpoint = "https://api.resend.com/emails";

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _log;

    public ResendEmailSender(HttpClient http, EmailOptions options, ILogger<ResendEmailSender> log)
    {
        _http = http;
        _options = options;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("Email não configurado (Email:From / Email:ResendApiKey) — email '{Subject}' não enviado.", message.Subject);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                from = _options.From,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ResendApiKey);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _log.LogError("Resend rejeitou o email '{Subject}': {Status} {Body}", message.Subject, (int)response.StatusCode, body);
            throw new InvalidOperationException($"Envio de email falhou ({(int)response.StatusCode}).");
        }
    }
}
