using System.Net;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Infrastructure;

public static class PasswordResetEmail
{
    public static EmailMessage Build(string to, string displayName, string link)
    {
        var greeting = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(displayName) ? "Olá," : $"Olá {displayName},");
        var safeLink = WebUtility.HtmlEncode(link);

        var html = $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:480px;margin:0 auto;color:#1f2937">
              <h2 style="margin:0 0 16px">Repor palavra-passe</h2>
              <p>{greeting}</p>
              <p>Recebemos um pedido para repor a palavra-passe da tua conta Mender. O link é válido durante 1 hora.</p>
              <p style="margin:24px 0">
                <a href="{safeLink}" style="background:#111827;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;display:inline-block">Escolher nova palavra-passe</a>
              </p>
              <p style="font-size:13px;color:#6b7280">Se não foste tu, ignora este email — a palavra-passe atual continua válida.</p>
            </div>
            """;

        var text = $"""
            Repor palavra-passe — Mender

            Recebemos um pedido para repor a palavra-passe da tua conta. Abre este link (válido 1 hora):
            {link}

            Se não foste tu, ignora este email.
            """;

        return new EmailMessage(to, "Repor a tua palavra-passe do Mender", html, text);
    }
}
