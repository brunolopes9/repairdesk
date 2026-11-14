using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

public sealed record SubscreverNewsletterRequest(string Email, string? Origem, bool AceitaRgpd, string? Website = null);
public sealed record TokenNewsletterRequest(string Token);
public sealed record SubscritorNewsletterDto(string Email, string Estado, string? Origem, DateTime CriadoEm, DateTime? ConfirmadoEm, DateTime? CanceladoEm);
public sealed record NewsletterResumoDto(int Ativos, int PorConfirmar, int Cancelados, IReadOnlyList<SubscritorNewsletterDto> Subscritores);

public interface INewsletterService
{
    /// <summary>Pede a subscrição: envia o email de confirmação. Responde sempre igual (não revela se o email já existe).</summary>
    Task SubscreverAsync(SubscreverNewsletterRequest req, CancellationToken ct = default);
    Task ConfirmarAsync(string token, CancellationToken ct = default);
    /// <summary>Deixa de receber. Idempotente; um token desconhecido também responde OK (não revela nada).</summary>
    Task CancelarAsync(string token, CancellationToken ct = default);
    Task<NewsletterResumoDto> ResumoAsync(CancellationToken ct = default);
}

/// <summary>
/// Sprint 661: newsletter da loja com confirmação dupla (RGPD — o consentimento tem de ser demonstrável).
/// O token de confirmação só existe no email (guarda-se o hash); o de cancelamento vai em todos os emails.
/// </summary>
public sealed class NewsletterService : INewsletterService
{
    /// <summary>Não reenvia a confirmação ao mesmo email antes disto (evita usar o formulário para spam).</summary>
    private static readonly TimeSpan IntervaloReenvio = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ValidadeConfirmacao = TimeSpan.FromDays(7);
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex OrigemRx = new(@"^[a-z0-9-]{1,40}$", RegexOptions.Compiled);

    private readonly ISubscritorNewsletterRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly StripeOptions _opt;
    private readonly ILogger<NewsletterService> _log;

    public NewsletterService(ISubscritorNewsletterRepository repo, ITenantContext tenant, IEmailSender email,
        IOptions<StripeOptions> opt, ILogger<NewsletterService> log)
    {
        _repo = repo;
        _tenant = tenant;
        _email = email;
        _opt = opt.Value;
        _log = log;
    }

    public async Task SubscreverAsync(SubscreverNewsletterRequest req, CancellationToken ct = default)
    {
        if (!req.AceitaRgpd) throw new ValidationException("consentimento", "Tens de aceitar receber a newsletter.");
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length > 160 || !EmailRx.IsMatch(email)) throw new ValidationException("email_invalido", "O email não parece válido.");
        var origem = req.Origem?.Trim().ToLowerInvariant();
        if (origem is not null && !OrigemRx.IsMatch(origem)) origem = null;
        var tenantId = _tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");
        var agora = DateTime.UtcNow;

        var s = await _repo.FindByEmailAsync(email, ct);
        if (s is { Ativo: true }) return; // já subscrito: nada a fazer, mesma resposta
        if (s?.ConfirmacaoEnviadaEm is { } enviada && agora - enviada < IntervaloReenvio) return;

        if (s is null)
        {
            s = new SubscritorNewsletter { TenantId = tenantId, Email = email, Origem = origem, TokenCancelamento = NovoToken() };
            await _repo.AddAsync(s, ct);
        }
        var token = NovoToken();
        s.TokenConfirmacaoHash = ContaLojaService.Hash(token);
        s.ConfirmacaoEnviadaEm = agora;
        await _repo.SaveAsync(ct);

        if (!_email.IsConfigured) { _log.LogWarning("Subscrição da newsletter pedida mas o email não está configurado"); return; }
        var site = _opt.SiteBase.TrimEnd('/');
        var confirmar = $"{site}/loja/newsletter?confirmar={Uri.EscapeDataString(token)}";
        var sair = $"{site}/loja/newsletter?cancelar={Uri.EscapeDataString(s.TokenCancelamento)}";
        var texto = "Olá! Pediste para receber a newsletter da LopesTech (novidades da loja, promoções e dicas, no máximo 2 vezes por mês).\n\n" +
                    $"Confirma aqui (válido 7 dias): {confirmar}\n\nSe não foste tu, ignora este email — sem confirmação não recebes nada.\n\nDeixar de receber: {sair}";
        try
        {
            await _email.SendAsync(new EmailMessage(email, "Confirma a tua subscrição — LopesTech",
                "<p>Olá! Pediste para receber a newsletter da LopesTech: novidades da loja, promoções e dicas, no máximo 2 vezes por mês.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(confirmar)}\">Confirmar subscrição</a> (válido 7 dias)</p>" +
                "<p>Se não foste tu, ignora este email — sem confirmação não recebes nada.</p>" +
                $"<p style=\"font-size:12px;color:#666\"><a href=\"{WebUtility.HtmlEncode(sair)}\">Deixar de receber</a></p>",
                texto), ct);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Email de confirmação da newsletter não enviado"); }
    }

    public async Task ConfirmarAsync(string token, CancellationToken ct = default)
    {
        var s = string.IsNullOrWhiteSpace(token) ? null : await _repo.FindByConfirmacaoHashAsync(ContaLojaService.Hash(token.Trim()), ct);
        if (s is null || s.ConfirmacaoEnviadaEm is not { } enviada || DateTime.UtcNow - enviada > ValidadeConfirmacao)
            throw new ValidationException("confirmacao_invalida", "O link expirou ou já foi usado. Subscreve outra vez.");
        s.ConfirmadoEm = DateTime.UtcNow;
        s.CanceladoEm = null;
        s.TokenConfirmacaoHash = null;
        await _repo.SaveAsync(ct);
    }

    public async Task CancelarAsync(string token, CancellationToken ct = default)
    {
        var s = string.IsNullOrWhiteSpace(token) ? null : await _repo.FindByCancelamentoAsync(token.Trim(), ct);
        if (s is null || s.CanceladoEm is not null) return;
        s.CanceladoEm = DateTime.UtcNow;
        s.TokenConfirmacaoHash = null;
        await _repo.SaveAsync(ct);
    }

    public async Task<NewsletterResumoDto> ResumoAsync(CancellationToken ct = default)
    {
        var todos = await _repo.ListAsync(ct);
        static string Estado(SubscritorNewsletter s) => s.CanceladoEm is not null ? "cancelado" : s.ConfirmadoEm is not null ? "ativo" : "por_confirmar";
        var dtos = todos.Select(s => new SubscritorNewsletterDto(s.Email, Estado(s), s.Origem, s.CreatedAt, s.ConfirmadoEm, s.CanceladoEm)).ToList();
        return new NewsletterResumoDto(dtos.Count(d => d.Estado == "ativo"), dtos.Count(d => d.Estado == "por_confirmar"),
            dtos.Count(d => d.Estado == "cancelado"), dtos);
    }

    private static string NovoToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
