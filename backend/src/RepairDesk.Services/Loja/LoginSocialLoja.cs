using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

/// <summary>O que o site precisa para mostrar os botões (só ids públicos — nunca segredos).</summary>
public sealed record ProvedoresLoginDto(string? GoogleClientId, string? FacebookAppId);
public sealed record EntrarSocialRequest(string Provedor, string Token);
public sealed record LoginSocialConfigDto(string? GoogleClientId, bool FacebookPronto, string? FacebookAppId);
public sealed record GuardarLoginSocialRequest(string? GoogleClientId);

/// <summary>Identidade confirmada pelo Google/Facebook.</summary>
public sealed record IdentidadeSocial(string Email, string Nome);

public interface ILoginSocialLoja
{
    Task<ProvedoresLoginDto> ProvedoresAsync(CancellationToken ct = default);
    Task<SessaoLojaDto> EntrarAsync(EntrarSocialRequest req, CancellationToken ct = default);
    Task<LoginSocialConfigDto> ConfigAsync(CancellationToken ct = default);
    Task<LoginSocialConfigDto> GuardarAsync(GuardarLoginSocialRequest req, CancellationToken ct = default);
}

/// <summary>
/// 23/11 (Bruno: «login com email/password, com Gmail e com Facebook»; Apple fica de fora — exige Apple Developer pago).
/// O site recebe um token do Google (ID token) ou do Facebook (access token) e o Mender confirma-o no próprio
/// fornecedor antes de entrar: Google → <c>oauth2.googleapis.com/tokeninfo</c> (aud = o nosso Client ID, email
/// confirmado); Facebook → <c>graph.facebook.com/debug_token</c> com a app «LopesTech Mender» (a mesma das redes
/// sociais) e depois <c>/me?fields=name,email</c>. Conta com o mesmo email → entra nela; senão cria-a.
/// </summary>
public sealed class LoginSocialLoja : ILoginSocialLoja
{
    private readonly HttpClient _http;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly IContaLojaService _contas;
    private readonly ILogger<LoginSocialLoja> _log;

    public LoginSocialLoja(HttpClient http, ITenantRepository tenants, ITenantContext tenant, ISecretProtector secrets,
        IContaLojaService contas, ILogger<LoginSocialLoja> log)
    {
        _http = http;
        _tenants = tenants;
        _tenant = tenant;
        _secrets = secrets;
        _contas = contas;
        _log = log;
    }

    private async Task<RepairDesk.Core.Entities.Tenant> Empresa(CancellationToken ct)
        => await _tenants.FindByIdAsync(_tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida."), ct)
           ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");

    public async Task<ProvedoresLoginDto> ProvedoresAsync(CancellationToken ct = default)
    {
        var t = await Empresa(ct);
        return new ProvedoresLoginDto(Vazio(t.LojaGoogleClientId),
            t.MetaAppSecretCipherText is not null ? Vazio(t.MetaAppId) : null);
    }

    public async Task<LoginSocialConfigDto> ConfigAsync(CancellationToken ct = default)
    {
        var t = await Empresa(ct);
        return new LoginSocialConfigDto(t.LojaGoogleClientId, t.MetaAppId is not null && t.MetaAppSecretCipherText is not null, t.MetaAppId);
    }

    public async Task<LoginSocialConfigDto> GuardarAsync(GuardarLoginSocialRequest req, CancellationToken ct = default)
    {
        var t = await Empresa(ct);
        var id = Vazio(req.GoogleClientId);
        if (id is not null && !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new ValidationException("google_client_id", "O Client ID do Google termina em «.apps.googleusercontent.com».");
        t.LojaGoogleClientId = id;
        await _tenants.SaveAsync(ct);
        return await ConfigAsync(ct);
    }

    public async Task<SessaoLojaDto> EntrarAsync(EntrarSocialRequest req, CancellationToken ct = default)
    {
        var token = (req.Token ?? "").Trim();
        if (token.Length is < 20 or > 4096) throw new ValidationException("token_invalido", "Não foi possível confirmar a conta. Tenta outra vez.");
        var t = await Empresa(ct);
        var id = (req.Provedor ?? "").Trim().ToLowerInvariant() switch
        {
            "google" => await Google(token, Vazio(t.LojaGoogleClientId) ?? throw Indisponivel("Google"), ct),
            "facebook" => await Facebook(token, Vazio(t.MetaAppId) ?? throw Indisponivel("Facebook"),
                t.MetaAppSecretCipherText is { } c ? _secrets.Unprotect(c) : throw Indisponivel("Facebook"), ct),
            _ => throw new ValidationException("provedor_invalido", "Forma de entrar desconhecida."),
        };
        return await _contas.EntrarComEmailConfirmadoAsync(id.Email, id.Nome, ct);
    }

    private async Task<IdentidadeSocial> Google(string idToken, string clientId, CancellationToken ct)
    {
        var j = await Get($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", "Google", ct);
        return IdentidadeGoogle(j, clientId, DateTimeOffset.UtcNow)
               ?? throw new ValidationException("google_invalido", "O Google não confirmou a conta. Tenta outra vez.");
    }

    /// <summary>Resposta do tokeninfo → identidade, só se o token é para a nossa app, de um emissor Google, não expirou
    /// e o email está confirmado.</summary>
    public static IdentidadeSocial? IdentidadeGoogle(JsonElement j, string clientId, DateTimeOffset agora)
    {
        string? S(string n) => j.ValueKind == JsonValueKind.Object && j.TryGetProperty(n, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText() : null;
        if (S("aud") != clientId) return null;
        if (S("iss") is not ("accounts.google.com" or "https://accounts.google.com")) return null;
        if (!string.Equals(S("email_verified"), "true", StringComparison.OrdinalIgnoreCase)) return null;
        if (!long.TryParse(S("exp"), out var exp) || DateTimeOffset.FromUnixTimeSeconds(exp) < agora) return null;
        var email = S("email");
        if (string.IsNullOrWhiteSpace(email)) return null;
        var nome = S("name");
        return new IdentidadeSocial(email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(nome) ? email.Split('@')[0] : nome.Trim());
    }

    private async Task<IdentidadeSocial> Facebook(string accessToken, string appId, string appSecret, CancellationToken ct)
    {
        var dbg = await Get($"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appId + "|" + appSecret)}", "Facebook", ct);
        if (!TokenFacebookValido(dbg, appId))
            throw new ValidationException("facebook_invalido", "O Facebook não confirmou a conta. Tenta outra vez.");
        var me = await Get($"https://graph.facebook.com/me?fields=name,email&access_token={Uri.EscapeDataString(accessToken)}", "Facebook", ct);
        var email = me.TryGetProperty("email", out var e) ? e.GetString() : null;
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("facebook_sem_email", "O Facebook não partilhou o teu email. Entra com o Google ou com email e palavra-passe.");
        var nome = me.TryGetProperty("name", out var n) ? n.GetString() : null;
        return new IdentidadeSocial(email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(nome) ? email.Split('@')[0] : nome.Trim());
    }

    /// <summary>debug_token: válido e emitido para a NOSSA app (um token de outra app não entra).</summary>
    public static bool TokenFacebookValido(JsonElement dbg, string appId)
        => dbg.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object
           && d.TryGetProperty("is_valid", out var v) && v.ValueKind == JsonValueKind.True
           && d.TryGetProperty("app_id", out var a) && a.ToString() == appId;

    private async Task<JsonElement> Get(string url, string quem, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogInformation("{Quem} recusou o token de login: HTTP {Status}", quem, (int)resp.StatusCode);
                throw new ValidationException("token_invalido", $"O {quem} não confirmou a conta. Tenta outra vez.");
            }
            return JsonDocument.Parse(texto).RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "{Quem} indisponível ao confirmar o login", quem);
            throw new ValidationException("provedor_indisponivel", $"O {quem} não respondeu. Tenta outra vez ou entra com email e palavra-passe.");
        }
    }

    private static ValidationException Indisponivel(string quem)
        => new("provedor_inativo", $"Entrar com {quem} ainda não está disponível. Entra com email e palavra-passe.");

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
