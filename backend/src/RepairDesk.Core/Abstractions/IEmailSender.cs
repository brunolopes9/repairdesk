namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Envio de emails transacionais (reset de palavra-passe, etc.).
/// Implementação em produção: Resend. Sem configuração, cai num sender que só regista em log.
/// </summary>
public interface IEmailSender
{
    /// <summary>False quando não há provider configurado — o email não sai do servidor.</summary>
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public sealed record EmailMessage(string To, string Subject, string Html, string Text);
