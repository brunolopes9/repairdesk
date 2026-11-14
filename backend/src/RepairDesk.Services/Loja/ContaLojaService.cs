using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

public sealed record RegistoLojaRequest(string Nome, string Email, string? Telefone, string Password, bool AceitaTermos);
public sealed record EntrarLojaRequest(string Email, string Password);
public sealed record PedirResetLojaRequest(string Email);
public sealed record RedefinirLojaRequest(string Token, string Password);
public sealed record SessaoLojaDto(string Token, DateTime ExpiraEm, ClienteLojaDto Cliente);
public sealed record ClienteLojaDto(string Nome, string Email, string? Telefone);

public interface IContaLojaService
{
    Task<SessaoLojaDto> RegistarAsync(RegistoLojaRequest req, CancellationToken ct = default);
    Task<SessaoLojaDto> EntrarAsync(EntrarLojaRequest req, CancellationToken ct = default);
    Task SairAsync(string? token, CancellationToken ct = default);
    /// <summary>O cliente da sessão, ou null se o token não for válido.</summary>
    Task<ClienteLoja?> ClienteDaSessaoAsync(string? token, CancellationToken ct = default);
    Task<IReadOnlyList<EncomendaPublicaDto>> EncomendasAsync(string? token, CancellationToken ct = default);
    /// <summary>Envia o link por email se a conta existir. Responde sempre igual (não revela se o email tem conta).</summary>
    Task PedirResetAsync(PedirResetLojaRequest req, CancellationToken ct = default);
    Task<SessaoLojaDto> RedefinirAsync(RedefinirLojaRequest req, CancellationToken ct = default);
}

/// <summary>
/// Sprint 659: contas de cliente da loja (email + palavra-passe). Sessões por token aleatório (só o hash na BD),
/// 30 dias; bloqueio de 15 min ao fim de 5 falhas; reset por email com token de 1 h.
/// Entrar com Google/Facebook/Apple fica para quando houver as apps OAuth.
/// </summary>
public sealed class ContaLojaService : IContaLojaService
{
    public static readonly TimeSpan DuracaoSessao = TimeSpan.FromDays(30);
    private static readonly TimeSpan DuracaoReset = TimeSpan.FromHours(1);
    private const int MaxFalhas = 5;
    private static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(15);
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly PasswordHasher<ClienteLoja> Hasher = new();

    private readonly IClienteLojaRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly StripeOptions _opt;
    private readonly IAuditLogger _audit;
    private readonly ILogger<ContaLojaService> _log;

    public ContaLojaService(IClienteLojaRepository repo, ITenantContext tenant, IEmailSender email, IOptions<StripeOptions> opt,
        IAuditLogger audit, ILogger<ContaLojaService> log)
    {
        _repo = repo;
        _tenant = tenant;
        _email = email;
        _opt = opt.Value;
        _audit = audit;
        _log = log;
    }

    public async Task<SessaoLojaDto> RegistarAsync(RegistoLojaRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");
        if (!req.AceitaTermos) throw new ValidationException("termos", "Tens de aceitar as condições e a política de privacidade.");
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is < 2 or > 120) throw new ValidationException("nome_invalido", "Escreve o teu nome.");
        var email = NormEmail(req.Email);
        ValidarPassword(req.Password);
        var tel = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim();
        if (tel is { Length: > 30 }) throw new ValidationException("telefone_invalido", "Telefone demasiado longo.");
        if (await _repo.FindByEmailAsync(email, ct) is not null)
            throw new ConflictException("email_existe", "Já existe uma conta com este email. Entra ou recupera a palavra-passe.");

        var c = new ClienteLoja { TenantId = tenantId, Nome = nome, Email = email, Telefone = tel, PasswordHash = "-" };
        c.PasswordHash = Hasher.HashPassword(c, req.Password);
        c.UltimoLoginEm = DateTime.UtcNow;
        await _repo.AddAsync(c, ct);
        var sessao = await NovaSessaoAsync(c, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(ClienteLoja), c.Id, new { c.Email }, tenantId, null, ct);
        return sessao;
    }

    public async Task<SessaoLojaDto> EntrarAsync(EntrarLojaRequest req, CancellationToken ct = default)
    {
        const string erro = "Email ou palavra-passe errados.";
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var c = await _repo.FindByEmailAsync(email, ct);
        if (c is null)
        {
            // Mesmo custo de tempo que uma verificação real (não revela se o email existe).
            Hasher.VerifyHashedPassword(new ClienteLoja { Nome = "-", Email = "-", PasswordHash = "-" }, FalsoHash, req.Password ?? "");
            throw new ValidationException("credenciais", erro);
        }
        if (c.BloqueadoAte is { } ate && ate > DateTime.UtcNow)
            throw new ValidationException("bloqueado", "Demasiadas tentativas. Espera 15 minutos ou recupera a palavra-passe.");
        var r = Hasher.VerifyHashedPassword(c, c.PasswordHash, req.Password ?? "");
        if (r == PasswordVerificationResult.Failed)
        {
            c.FalhasLogin++;
            if (c.FalhasLogin >= MaxFalhas) { c.BloqueadoAte = DateTime.UtcNow.Add(Bloqueio); c.FalhasLogin = 0; }
            await _repo.SaveAsync(ct);
            throw new ValidationException("credenciais", erro);
        }
        if (r == PasswordVerificationResult.SuccessRehashNeeded) c.PasswordHash = Hasher.HashPassword(c, req.Password!);
        c.FalhasLogin = 0;
        c.BloqueadoAte = null;
        c.UltimoLoginEm = DateTime.UtcNow;
        var sessao = await NovaSessaoAsync(c, ct);
        await _repo.SaveAsync(ct);
        return sessao;
    }

    public async Task SairAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var s = await _repo.FindSessaoAsync(Hash(token), DateTime.UtcNow, ct);
        if (s is null) return;
        _repo.RemoveSessao(s);
        await _repo.SaveAsync(ct);
    }

    public async Task<ClienteLoja?> ClienteDaSessaoAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        return (await _repo.FindSessaoAsync(Hash(token.Trim()), DateTime.UtcNow, ct))?.Cliente;
    }

    public async Task<IReadOnlyList<EncomendaPublicaDto>> EncomendasAsync(string? token, CancellationToken ct = default)
    {
        var c = await ClienteDaSessaoAsync(token, ct) ?? throw new ForbiddenException("sessao", "Inicia sessão para veres as tuas encomendas.");
        return (await _repo.EncomendasAsync(c.Id, ct)).Select(e => new EncomendaPublicaDto(
            e.Referencia, e.Estado, EncomendaLojaService.EstadoTexto(e.Estado), e.CreatedAt, c.Nome,
            e.Linhas.Select(l => new EncomendaPublicaLinhaDto(l.Titulo, l.Slug, l.Quantidade, l.PrecoUnitarioCents / 100m)).ToList(),
            e.SubtotalCents / 100m, e.PortesCents / 100m, e.TotalCents / 100m, e.Transportadora, e.NumeroSeguimento,
            e.PagaEm, e.EnviadaEm, e.EntregueEm)).ToList();
    }

    public async Task PedirResetAsync(PedirResetLojaRequest req, CancellationToken ct = default)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var c = await _repo.FindByEmailAsync(email, ct);
        if (c is null) return;
        var token = NovoToken();
        c.ResetTokenHash = Hash(token);
        c.ResetExpiraEm = DateTime.UtcNow.Add(DuracaoReset);
        await _repo.SaveAsync(ct);
        if (!_email.IsConfigured) { _log.LogWarning("Reset de conta da loja pedido mas o email não está configurado"); return; }
        var link = $"{_opt.SiteBase.TrimEnd('/')}/loja/conta?reset={Uri.EscapeDataString(token)}";
        var texto = $"Olá {c.Nome.Split(' ')[0]}, pediste para mudar a palavra-passe da tua conta na loja LopesTech.\n\n" +
                    $"Abre este link (válido 1 hora): {link}\n\nSe não foste tu, ignora este email.";
        try
        {
            await _email.SendAsync(new EmailMessage(c.Email, "Mudar a palavra-passe — loja LopesTech",
                $"<p>{System.Net.WebUtility.HtmlEncode(texto.Split("\n\n")[0])}</p><p><a href=\"{link}\">Escolher nova palavra-passe</a> (válido 1 hora)</p><p>Se não foste tu, ignora este email.</p>",
                texto), ct);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Email de reset da loja não enviado"); }
    }

    public async Task<SessaoLojaDto> RedefinirAsync(RedefinirLojaRequest req, CancellationToken ct = default)
    {
        ValidarPassword(req.Password);
        var c = string.IsNullOrWhiteSpace(req.Token) ? null : await _repo.FindByResetHashAsync(Hash(req.Token.Trim()), ct);
        if (c is null || c.ResetExpiraEm is not { } exp || exp < DateTime.UtcNow)
            throw new ValidationException("reset_invalido", "O link expirou ou já foi usado. Pede um novo.");
        c.PasswordHash = Hasher.HashPassword(c, req.Password);
        c.ResetTokenHash = null;
        c.ResetExpiraEm = null;
        c.FalhasLogin = 0;
        c.BloqueadoAte = null;
        await _repo.RemoverSessoesAsync(c.Id, ct);
        var sessao = await NovaSessaoAsync(c, ct);
        await _repo.SaveAsync(ct);
        return sessao;
    }

    // ---- auxiliares ----

    private async Task<SessaoLojaDto> NovaSessaoAsync(ClienteLoja c, CancellationToken ct)
    {
        var token = NovoToken();
        var s = new ClienteLojaSessao { TenantId = c.TenantId, ClienteId = c.Id, TokenHash = Hash(token), ExpiraEm = DateTime.UtcNow.Add(DuracaoSessao) };
        await _repo.AddSessaoAsync(s, ct);
        return new SessaoLojaDto(token, s.ExpiraEm, new ClienteLojaDto(c.Nome, c.Email, c.Telefone));
    }

    private static string NormEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length > 160 || !EmailRx.IsMatch(e)) throw new ValidationException("email_invalido", "O email não parece válido.");
        return e;
    }

    private static void ValidarPassword(string? p)
    {
        if (p is null || p.Length < 8) throw new ValidationException("password_curta", "A palavra-passe precisa de pelo menos 8 caracteres.");
        if (p.Length > 200) throw new ValidationException("password_longa", "Palavra-passe demasiado longa.");
    }

    private static string NovoToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static readonly string FalsoHash = Hasher.HashPassword(new ClienteLoja { Nome = "-", Email = "-", PasswordHash = "-" }, "nao-existe-" + Guid.NewGuid());
}
