using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Conteudo.Redes;

public sealed record RedeLigadaDto(RedeSocial Rede, string ContaNome, string ContaId, DateTime LigadaEm, DateTime? TokenExpiraEm, bool Valida, string? UltimoErro);

public sealed record RedesSociaisEstadoDto(
    string? MetaAppId,
    bool MetaAppSecretDefinido,
    string? MetaLoginConfigId,
    string? ThreadsAppId,
    bool ThreadsAppSecretDefinido,
    bool AutoPublicar,
    string RedirectMeta,
    string RedirectThreads,
    IReadOnlyList<RedeLigadaDto> Ligacoes);

/// <summary>Secrets null = manter o que está guardado.</summary>
public sealed record GuardarAppsRedesRequest(string? MetaAppId, string? MetaAppSecret, string? ThreadsAppId, string? ThreadsAppSecret, string? MetaLoginConfigId = null);

public interface IRedesSociaisService
{
    Task<RedesSociaisEstadoDto> EstadoAsync(CancellationToken ct = default);
    Task<RedesSociaisEstadoDto> GuardarAppsAsync(GuardarAppsRedesRequest req, CancellationToken ct = default);
    Task<RedesSociaisEstadoDto> DefinirAutoPublicarAsync(bool ligado, CancellationToken ct = default);
    /// <summary>URL do diálogo de autorização ("meta" ou "threads") com state assinado.</summary>
    Task<string> UrlLigarAsync(string plataforma, CancellationToken ct = default);
    /// <summary>Troca o código por tokens e grava as ligações. Devolve o caminho do frontend para onde voltar.</summary>
    Task<string> CallbackAsync(string plataforma, string? code, string? state, string? erro, CancellationToken ct = default);
    Task<RedesSociaisEstadoDto> DesligarAsync(RedeSocial rede, CancellationToken ct = default);
    /// <summary>
    /// Liga o Threads com um token gerado no painel da Meta («User Token Generator» dos testers),
    /// sem passar pelo OAuth nem precisar do App Secret.
    /// </summary>
    Task<RedesSociaisEstadoDto> LigarThreadsComTokenAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// Sprint 599 (Doc 95 Fase B): ligação das contas por OAuth. As credenciais das apps e os tokens
/// ficam cifrados (<see cref="ISecretProtector"/>) e nunca são devolvidos ao frontend.
/// </summary>
public sealed class RedesSociaisService : IRedesSociaisService
{
    private const string VoltarPara = "/definicoes/redes-sociais";
    private static readonly TimeSpan ValidadeState = TimeSpan.FromMinutes(15);

    private static readonly string[] ScopesMeta =
    {
        "pages_show_list", "pages_read_engagement", "pages_manage_posts",
        "instagram_basic", "instagram_content_publish", "business_management",
    };

    private readonly IConteudoRepository _repo;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly ISecretProtector _secrets;
    private readonly IAuditLogger _audit;
    private readonly MetaGraphClient _g;
    private readonly string _baseUrl;
    private readonly byte[] _stateKey;

    public RedesSociaisService(
        IConteudoRepository repo,
        ITenantRepository tenants,
        ITenantContext tenant,
        ICurrentUser user,
        ISecretProtector secrets,
        IAuditLogger audit,
        MetaGraphClient g,
        IConfiguration config)
    {
        _repo = repo;
        _tenants = tenants;
        _tenant = tenant;
        _user = user;
        _secrets = secrets;
        _audit = audit;
        _g = g;
        _baseUrl = (config["Frontend:BaseUrl"] ?? "https://app.lopestech.pt").TrimEnd('/');
        _stateKey = Encoding.UTF8.GetBytes(config["Storage:SignedUrlKey"] ?? config["Jwt:SigningKey"] ?? "repairdesk-dev-signed-url-key-change-me");
    }

    private string Redirect(string plataforma) => $"{_baseUrl}/api/redes/{plataforma}/callback";

    public async Task<RedesSociaisEstadoDto> EstadoAsync(CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        var ligacoes = await _repo.ListLigacoesAsync(t.Id, ct);
        return new RedesSociaisEstadoDto(
            t.MetaAppId, t.MetaAppSecretCipherText is not null, t.MetaLoginConfigId,
            t.ThreadsAppId, t.ThreadsAppSecretCipherText is not null,
            t.ConteudoAutoPublicar, Redirect("meta"), Redirect("threads"),
            ligacoes.Select(l => new RedeLigadaDto(l.Rede, l.ContaNome, l.ContaId, l.LigadaEm, l.TokenExpiraEm, l.Valida, l.UltimoErro)).ToList());
    }

    public async Task<RedesSociaisEstadoDto> GuardarAppsAsync(GuardarAppsRedesRequest req, CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        t.MetaAppId = AppId(req.MetaAppId);
        t.ThreadsAppId = AppId(req.ThreadsAppId);
        t.MetaLoginConfigId = AppId(req.MetaLoginConfigId);
        if (!string.IsNullOrWhiteSpace(req.MetaAppSecret)) t.MetaAppSecretCipherText = _secrets.Protect(Secret(req.MetaAppSecret));
        if (!string.IsNullOrWhiteSpace(req.ThreadsAppSecret)) t.ThreadsAppSecretCipherText = _secrets.Protect(Secret(req.ThreadsAppSecret));
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, "RedesSociais", t.Id,
            new { t.MetaAppId, t.ThreadsAppId, metaSecret = req.MetaAppSecret is not null, threadsSecret = req.ThreadsAppSecret is not null }, t.Id, _user.UserId, ct);
        return await EstadoAsync(ct);
    }

    public async Task<RedesSociaisEstadoDto> DefinirAutoPublicarAsync(bool ligado, CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        if (ligado && (await _repo.ListLigacoesAsync(t.Id, ct)).Count(l => l.Valida) == 0)
            throw new ValidationException("sem_contas", "Liga primeiro pelo menos uma conta.");
        t.ConteudoAutoPublicar = ligado;
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, "RedesSociais", t.Id, new { autoPublicar = ligado }, t.Id, _user.UserId, ct);
        return await EstadoAsync(ct);
    }

    public async Task<string> UrlLigarAsync(string plataforma, CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        var uid = _user.UserId ?? throw new ForbiddenException("so_utilizador", "Só o dono liga contas.");
        var state = AssinarState(plataforma, t.Id, uid);
        return plataforma switch
        {
            "meta" => $"https://www.facebook.com/{_g.GraphBase.Split('/').Last()}/dialog/oauth?client_id={Uri.EscapeDataString(t.MetaAppId ?? throw SemApp("Meta"))}"
                      + $"&redirect_uri={Uri.EscapeDataString(Redirect("meta"))}&state={Uri.EscapeDataString(state)}&response_type=code"
                      // Apps com «Facebook Login for Business» pedem as permissões pela configuração (config_id), não por scope.
                      + (t.MetaLoginConfigId is { } cfg
                          ? $"&config_id={Uri.EscapeDataString(cfg)}&override_default_response_type=true"
                          : $"&scope={Uri.EscapeDataString(string.Join(',', ScopesMeta))}"),
            "threads" => $"https://threads.net/oauth/authorize?client_id={Uri.EscapeDataString(t.ThreadsAppId ?? throw SemApp("Threads"))}"
                         + $"&redirect_uri={Uri.EscapeDataString(Redirect("threads"))}&state={Uri.EscapeDataString(state)}"
                         + "&response_type=code&scope=threads_basic,threads_content_publish",
            _ => throw new ValidationException("plataforma_invalida", "Plataforma desconhecida."),
        };
    }

    public async Task<string> CallbackAsync(string plataforma, string? code, string? state, string? erro, CancellationToken ct = default)
    {
        Guid tenantId, userId;
        try
        {
            (tenantId, userId) = ValidarState(plataforma, state);
        }
        catch (ForbiddenException)
        {
            return $"{VoltarPara}?erro={Uri.EscapeDataString("O pedido de ligação expirou — tenta outra vez.")}";
        }
        if (!string.IsNullOrEmpty(erro) || string.IsNullOrEmpty(code))
            return $"{VoltarPara}?erro={Uri.EscapeDataString("Ligação cancelada.")}";

        var t = await _tenants.FindByIdAsync(tenantId, ct) ?? throw new NotFoundException("Tenant", tenantId);
        try
        {
            var ligadas = plataforma == "meta"
                ? await LigarMetaAsync(t, code, userId, ct)
                : await LigarThreadsAsync(t, code, userId, ct);
            await _repo.SaveAsync(ct);
            await _audit.LogAsync(AuditAction.Update, "RedesSociais", t.Id, new { ligadas }, t.Id, userId, ct);
            return $"{VoltarPara}?ligado={Uri.EscapeDataString(string.Join(", ", ligadas))}";
        }
        catch (RedeSocialException ex)
        {
            return $"{VoltarPara}?erro={Uri.EscapeDataString(ex.Message)}";
        }
    }

    public async Task<RedesSociaisEstadoDto> DesligarAsync(RedeSocial rede, CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        foreach (var l in (await _repo.ListLigacoesAsync(t.Id, ct)).Where(l => l.Rede == rede)) _repo.Remove(l);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, "RedesSociais", t.Id, new { rede = rede.ToString() }, t.Id, _user.UserId, ct);
        return await EstadoAsync(ct);
    }

    public async Task<RedesSociaisEstadoDto> LigarThreadsComTokenAsync(string token, CancellationToken ct = default)
    {
        var t = await TenantAtual(ct);
        var uid = _user.UserId ?? throw new ForbiddenException("so_utilizador", "Só o dono liga contas.");
        var tk = (token ?? "").Trim();
        if (tk.Length is < 20 or > 1000 || tk.Any(char.IsWhiteSpace))
            throw new ValidationException("token_invalido", "Esse token não parece completo — copia-o outra vez.");
        try
        {
            var eu = await _g.GetAsync($"{_g.ThreadsBase}/me", new Dictionary<string, string?> { ["fields"] = "id,username", ["access_token"] = tk }, ct);
            // Renovar logo dá um token de 60 dias e diz quando expira (os do gerador já são de longa duração).
            DateTime expira;
            try
            {
                var r = await _g.GetAsync("https://graph.threads.net/refresh_access_token",
                    new Dictionary<string, string?> { ["grant_type"] = "th_refresh_token", ["access_token"] = tk }, ct);
                tk = MetaGraphClient.Str(r, "access_token");
                expira = DateTime.UtcNow.AddSeconds(long.TryParse(MetaGraphClient.StrOuNull(r, "expires_in"), out var s) ? s : 60 * 86400);
            }
            catch (RedeSocialException)
            {
                expira = DateTime.UtcNow.AddDays(55); // conservador; o publicador avisa antes de expirar
            }
            var nome = "@" + (MetaGraphClient.StrOuNull(eu, "username") ?? "threads");
            await Gravar(t.Id, RedeSocial.Threads, MetaGraphClient.Str(eu, "id"), nome, _secrets.Protect(tk), expira, uid, ct);
            await _repo.SaveAsync(ct);
            await _audit.LogAsync(AuditAction.Update, "RedesSociais", t.Id, new { ligadas = new[] { $"Threads ({nome}) por token" } }, t.Id, uid, ct);
        }
        catch (RedeSocialException ex)
        {
            throw new ValidationException("token_recusado", $"O Threads recusou esse token: {ex.Message}");
        }
        return await EstadoAsync(ct);
    }

    // ---- OAuth -------------------------------------------------------------------------------

    private async Task<List<string>> LigarMetaAsync(Tenant t, string code, Guid userId, CancellationToken ct)
    {
        var appId = t.MetaAppId ?? throw new RedeSocialException("Falta o App ID da Meta.");
        var secret = t.MetaAppSecretCipherText is { } c ? _secrets.Unprotect(c) : throw new RedeSocialException("Falta o App Secret da Meta.");

        var curto = await _g.GetAsync($"{_g.GraphBase}/oauth/access_token", new Dictionary<string, string?>
        {
            ["client_id"] = appId, ["client_secret"] = secret, ["redirect_uri"] = Redirect("meta"), ["code"] = code,
        }, ct);
        var longo = await _g.GetAsync($"{_g.GraphBase}/oauth/access_token", new Dictionary<string, string?>
        {
            ["grant_type"] = "fb_exchange_token", ["client_id"] = appId, ["client_secret"] = secret,
            ["fb_exchange_token"] = MetaGraphClient.Str(curto, "access_token"),
        }, ct);
        // Tokens de Página obtidos a partir de um user token de longa duração não expiram.
        var paginas = await _g.GetAsync($"{_g.GraphBase}/me/accounts", new Dictionary<string, string?>
        {
            ["fields"] = "id,name,access_token,instagram_business_account{id,username}",
            ["access_token"] = MetaGraphClient.Str(longo, "access_token"),
        }, ct);
        var lista = paginas.TryGetProperty("data", out var d) ? d.EnumerateArray().ToList() : new();
        if (lista.Count == 0)
            throw new RedeSocialException("Não escolheste nenhuma Página — liga outra vez e marca a Página da loja.");
        if (lista.Count > 1)
            throw new RedeSocialException($"Escolheste {lista.Count} Páginas ({string.Join(", ", lista.Select(p => MetaGraphClient.StrOuNull(p, "name")))}). Liga outra vez e marca só a da loja.");

        var pagina = lista[0];
        var tokenPagina = _secrets.Protect(MetaGraphClient.Str(pagina, "access_token"));
        var ligadas = new List<string>();
        await Gravar(t.Id, RedeSocial.Facebook, MetaGraphClient.Str(pagina, "id"), MetaGraphClient.Str(pagina, "name"), tokenPagina, null, userId, ct);
        ligadas.Add($"Facebook ({MetaGraphClient.Str(pagina, "name")})");
        if (pagina.TryGetProperty("instagram_business_account", out var ig))
        {
            var nome = "@" + (MetaGraphClient.StrOuNull(ig, "username") ?? "instagram");
            await Gravar(t.Id, RedeSocial.Instagram, MetaGraphClient.Str(ig, "id"), nome, tokenPagina, null, userId, ct);
            ligadas.Add($"Instagram ({nome})");
        }
        return ligadas;
    }

    private async Task<List<string>> LigarThreadsAsync(Tenant t, string code, Guid userId, CancellationToken ct)
    {
        var appId = t.ThreadsAppId ?? throw new RedeSocialException("Falta o App ID do Threads.");
        var secret = t.ThreadsAppSecretCipherText is { } c ? _secrets.Unprotect(c) : throw new RedeSocialException("Falta o App Secret do Threads.");

        var curto = await _g.PostAsync("https://graph.threads.net/oauth/access_token", new Dictionary<string, string?>
        {
            ["client_id"] = appId, ["client_secret"] = secret, ["grant_type"] = "authorization_code",
            ["redirect_uri"] = Redirect("threads"), ["code"] = code,
        }, ct);
        var longo = await _g.GetAsync("https://graph.threads.net/access_token", new Dictionary<string, string?>
        {
            ["grant_type"] = "th_exchange_token", ["client_secret"] = secret, ["access_token"] = MetaGraphClient.Str(curto, "access_token"),
        }, ct);
        var token = MetaGraphClient.Str(longo, "access_token");
        var expiraEm = DateTime.UtcNow.AddSeconds(long.TryParse(MetaGraphClient.StrOuNull(longo, "expires_in"), out var s) ? s : 60 * 86400);
        var eu = await _g.GetAsync($"{_g.ThreadsBase}/me", new Dictionary<string, string?> { ["fields"] = "id,username", ["access_token"] = token }, ct);
        var nome = "@" + (MetaGraphClient.StrOuNull(eu, "username") ?? "threads");
        await Gravar(t.Id, RedeSocial.Threads, MetaGraphClient.Str(eu, "id"), nome, _secrets.Protect(token), expiraEm, userId, ct);
        return new List<string> { $"Threads ({nome})" };
    }

    /// <summary>Ligar outra vez atualiza a linha existente (o índice único por rede não permite duas).</summary>
    private async Task Gravar(Guid tenantId, RedeSocial rede, string contaId, string nome, string tokenCifrado, DateTime? expira, Guid userId, CancellationToken ct)
    {
        var l = (await _repo.ListLigacoesAsync(tenantId, ct)).FirstOrDefault(x => x.Rede == rede);
        if (l is null)
        {
            await _repo.AddAsync(new RedeSocialLigacao
            {
                TenantId = tenantId, Rede = rede, ContaId = contaId, ContaNome = nome,
                TokenCipherText = tokenCifrado, TokenExpiraEm = expira, LigadaPorUserId = userId,
            }, ct);
            return;
        }
        l.ContaId = contaId;
        l.ContaNome = nome;
        l.TokenCipherText = tokenCifrado;
        l.TokenExpiraEm = expira;
        l.LigadaEm = DateTime.UtcNow;
        l.LigadaPorUserId = userId;
        l.Valida = true;
        l.UltimoErro = null;
    }

    // ---- state assinado -------------------------------------------------------------------

    private string AssinarState(string plataforma, Guid tenantId, Guid userId)
    {
        var exp = DateTimeOffset.UtcNow.Add(ValidadeState).ToUnixTimeSeconds();
        var payload = $"{plataforma}|{tenantId:N}|{userId:N}|{exp}";
        return $"{B64(Encoding.UTF8.GetBytes(payload))}.{B64(Hmac(payload))}";
    }

    private (Guid TenantId, Guid UserId) ValidarState(string plataforma, string? state)
    {
        var partes = (state ?? "").Split('.');
        if (partes.Length != 2) throw new ForbiddenException("state_invalido", "Pedido inválido.");
        string payload;
        try { payload = Encoding.UTF8.GetString(DeB64(partes[0])); }
        catch (FormatException) { throw new ForbiddenException("state_invalido", "Pedido inválido."); }
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(B64(Hmac(payload))), Encoding.ASCII.GetBytes(partes[1])))
            throw new ForbiddenException("state_invalido", "Pedido inválido.");
        var c = payload.Split('|');
        if (c.Length != 4 || c[0] != plataforma || !long.TryParse(c[3], out var exp) || DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow)
            throw new ForbiddenException("state_expirado", "Pedido expirado.");
        return (Guid.Parse(c[1]), Guid.Parse(c[2]));
    }

    private byte[] Hmac(string payload)
    {
        using var h = new HMACSHA256(_stateKey);
        return h.ComputeHash(Encoding.UTF8.GetBytes($"redes:{payload}"));
    }

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DeB64(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(t.PadRight(t.Length + (4 - t.Length % 4) % 4, '='));
    }

    // ---- utilitários ------------------------------------------------------------------------

    private async Task<Tenant> TenantAtual(CancellationToken ct)
    {
        var id = _tenant.TenantId ?? throw new ForbiddenException("tenant_required", "Sem tenant.");
        return await _tenants.FindByIdAsync(id, ct) ?? throw new NotFoundException("Tenant", id);
    }

    private static ValidationException SemApp(string qual)
        => new("app_em_falta", $"Escreve primeiro o App ID e o App Secret do {qual}.");

    private static string? AppId(string? v)
    {
        var s = v?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Length > 50 || !s.All(char.IsDigit)) throw new ValidationException("app_id_invalido", "O App ID tem só algarismos.");
        return s;
    }

    private static string Secret(string v)
    {
        var s = v.Trim();
        if (s.Length is < 16 or > 200) throw new ValidationException("app_secret_invalido", "O App Secret parece incompleto.");
        return s;
    }
}
