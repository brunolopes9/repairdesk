using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

/// <summary>
/// Sprint 690: a loja online cobra pelo ifthenpay (decisão do Bruno — MB WAY, Multibanco, cartão, Google Pay e Apple Pay
/// numa só página: «Pay by Link», <c>POST https://api.ifthenpay.com/gateway/pinpay/{GATEWAY_KEY}</c>).
/// A confirmação NÃO é o regresso do cliente ao site: é o callback do ifthenpay com a chave anti-phishing
/// (ver <c>PublicLojaController.IfthenpayCallback</c>). As chaves são da empresa, cifradas na BD (Definições).
/// </summary>
public sealed class IfthenpayLojaCheckout
{
    public const string Url = "https://api.ifthenpay.com/gateway/pinpay/";
    private readonly HttpClient _http;
    private readonly ILogger<IfthenpayLojaCheckout> _log;

    public IfthenpayLojaCheckout(HttpClient http, ILogger<IfthenpayLojaCheckout> log)
    {
        _http = http;
        _log = log;
    }

    /// <summary>A encomenda guarda «ifp:LT-XXXXXX» no campo da sessão — o callback traz o id (a referência).</summary>
    public static string SessaoDe(string referencia) => "ifp:" + referencia;

    public async Task<StripeSessao> CriarSessaoAsync(string gatewayKey, StripeSessaoPedido p, CancellationToken ct)
        => await CriarSessaoAsync(gatewayKey, null, p, ct);

    /// <param name="contas">Sprint 700: «MB|…;MBWAY|…;CCARD|…» — os métodos que a página mostra. Uma Gateway Key
    /// «dinâmica» não tem métodos no ifthenpay; sem isto a página abre só com o valor e sem forma de pagar.</param>
    public async Task<StripeSessao> CriarSessaoAsync(string gatewayKey, string? contas, StripeSessaoPedido p, CancellationToken ct)
    {
        var totalCents = p.Linhas.Sum(l => l.PrecoUnitarioCents * l.Quantidade);
        var corpo = new Dictionary<string, string>
        {
            ["id"] = p.Referencia, // ≤ 15 caracteres («LT-» + 6)
            ["amount"] = (totalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["description"] = $"Encomenda {p.Referencia} — LopesTech",
            ["lang"] = "pt",
            // Multibanco: a referência vale 3 dias; MB WAY e cartão pagam-se logo.
            ["expiredate"] = DateTime.UtcNow.AddDays(3).ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ["success_url"] = p.UrlSucesso,
            ["error_url"] = p.UrlCancelar,
            ["cancel_url"] = p.UrlCancelar,
            ["btnCloseUrl"] = p.UrlCancelar,
            ["btnCloseLabel"] = "Voltar à loja",
            ["otp"] = "true",
        };
        if (!string.IsNullOrWhiteSpace(contas)) corpo["accounts"] = contas;
        HttpResponseMessage resp;
        try { resp = await _http.PostAsJsonAsync(Url + Uri.EscapeDataString(gatewayKey), corpo, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "ifthenpay indisponível ao criar o link da encomenda {Ref}", p.Referencia);
            throw new ValidationException("pagamento_indisponivel", "O serviço de pagamento não respondeu. Tenta outra vez dentro de momentos.");
        }
        var texto = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("ifthenpay recusou o link da encomenda {Ref}: HTTP {Status} {Corpo}", p.Referencia, (int)resp.StatusCode, texto.Length > 300 ? texto[..300] : texto);
            throw new ValidationException("pagamento_recusado", "Não foi possível abrir a página de pagamento. Fala connosco pelo WhatsApp 920 182 332.");
        }
        string? url = null;
        try
        {
            using var doc = JsonDocument.Parse(texto);
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (string.Equals(prop.Name, "RedirectUrl", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                    url = prop.Value.GetString();
        }
        catch (JsonException) { /* tratado abaixo */ }
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("ifthenpay sem RedirectUrl para {Ref}: {Corpo}", p.Referencia, texto.Length > 300 ? texto[..300] : texto);
            throw new ValidationException("pagamento_recusado", "Não foi possível abrir a página de pagamento. Fala connosco pelo WhatsApp 920 182 332.");
        }
        return new StripeSessao(SessaoDe(p.Referencia), url);
    }

    /// <summary>Comparação em tempo constante da chave anti-phishing que vem no callback.</summary>
    public static bool ChaveValida(string? recebida, string? esperada)
        => !string.IsNullOrEmpty(recebida) && !string.IsNullOrEmpty(esperada)
           && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebida), Encoding.UTF8.GetBytes(esperada));

    /// <summary>O valor do callback («21.50») bate com o total da encomenda?</summary>
    public static bool ValorBate(string? amount, long totalCents)
        => decimal.TryParse((amount ?? "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
           && Math.Abs(Math.Round(v * 100) - totalCents) <= 1;
}

/// <summary>
/// Sprint 690: escolhe quem cobra, por empresa — ifthenpay quando a empresa tem a Gateway Key; senão o Stripe
/// (se o servidor tiver chaves). Assim a mudança é só colar as chaves em Definições.
/// </summary>
public sealed class PagamentoLojaRouter : IStripeCheckout
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly IfthenpayLojaCheckout _ifthenpay;
    private readonly StripeCheckoutClient _stripe;

    public PagamentoLojaRouter(ITenantRepository tenants, ITenantContext tenant, ISecretProtector secrets,
        IfthenpayLojaCheckout ifthenpay, StripeCheckoutClient stripe)
    {
        _tenants = tenants;
        _tenant = tenant;
        _secrets = secrets;
        _ifthenpay = ifthenpay;
        _stripe = stripe;
    }

    private async Task<(string Key, string? Contas)?> GatewayAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } id) return null;
        var t = await _tenants.FindByIdAsync(id, ct);
        if (t?.IfthenpayGatewayKeyCipherText is not { } c || t.IfthenpayAntiPhishingKeyCipherText is null) return null;
        return (_secrets.Unprotect(c), t.IfthenpayContasCipherText is { } cc ? _secrets.Unprotect(cc) : null);
    }

    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
        => await GatewayAsync(ct) is not null || _stripe.Configurado;

    public async Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default)
        => await GatewayAsync(ct) is { } g
            ? await _ifthenpay.CriarSessaoAsync(g.Key, g.Contas, pedido, ct)
            : await _stripe.CriarSessaoAsync(pedido, ct);
}

/// <summary>Sprint 700: os métodos que o ifthenpay aceita no «accounts» do Pay by Link (código → nome para o ecrã).</summary>
public static class MetodosIfthenpay
{
    public static readonly IReadOnlyList<(string Codigo, string Nome)> Todos =
    [
        ("MB", "Multibanco"), ("MBWAY", "MB WAY"), ("CCARD", "Cartão"), ("GOOGLE", "Google Pay"), ("APPLE", "Apple Pay"),
    ];

    public static Dictionary<string, string> Ler(string? contas)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parte in (contas ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = parte.IndexOf('|');
            if (i > 0 && i < parte.Length - 1) d[parte[..i]] = parte[(i + 1)..];
        }
        return d;
    }

    /// <summary>Por ordem fixa (Multibanco primeiro) para a página do ifthenpay abrir sempre igual.</summary>
    public static string? Escrever(Dictionary<string, string> d)
    {
        var s = string.Join(';', Todos.Where(m => d.ContainsKey(m.Codigo)).Select(m => $"{m.Codigo}|{d[m.Codigo]}"));
        return s.Length == 0 ? null : s;
    }
}

public sealed record PagamentosLojaEstadoDto(bool GatewayKeyDefinida, bool AntiPhishingDefinida, bool Ativo, string? CallbackUrl,
    IReadOnlyList<string>? MetodosDefinidos = null);
/// <param name="Metodos">Sprint 700: chave de cada método por código (MB, MBWAY, CCARD, GOOGLE, APPLE). Vazio = não mexe;
/// «-» = tira esse método.</param>
public sealed record GuardarPagamentosLojaRequest(string? GatewayKey, string? AntiPhishingKey, bool Remover = false,
    Dictionary<string, string?>? Metodos = null);

public interface IPagamentosLojaService
{
    Task<PagamentosLojaEstadoDto> EstadoAsync(CancellationToken ct = default);
    Task<PagamentosLojaEstadoDto> GuardarAsync(GuardarPagamentosLojaRequest req, CancellationToken ct = default);
}

/// <summary>Sprint 690: Definições → Pagamentos da loja (ifthenpay). As chaves nunca voltam ao browser — só «definida».</summary>
public sealed class PagamentosLojaService : IPagamentosLojaService
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly ISecretProtector _secrets;
    private readonly IAuditLogger _audit;
    private readonly string _apiBase;

    public PagamentosLojaService(ITenantRepository tenants, ITenantContext tenant, ICurrentUser user, ISecretProtector secrets,
        IAuditLogger audit, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _tenants = tenants;
        _tenant = tenant;
        _user = user;
        _secrets = secrets;
        _audit = audit;
        _apiBase = (config["Frontend:BaseUrl"] ?? "https://app.lopestech.pt").TrimEnd('/');
    }

    private async Task<Tenant> Atual(CancellationToken ct)
        => await _tenants.FindByIdAsync(_tenant.TenantId ?? throw new ForbiddenException("sem_empresa", "Sem empresa."), ct)
           ?? throw new NotFoundException("Tenant", _tenant.TenantId!.Value);

    private PagamentosLojaEstadoDto Dto(Tenant t) => new(
        t.IfthenpayGatewayKeyCipherText is not null, t.IfthenpayAntiPhishingKeyCipherText is not null,
        t.IfthenpayGatewayKeyCipherText is not null && t.IfthenpayAntiPhishingKeyCipherText is not null,
        string.IsNullOrWhiteSpace(t.IntakeSlug) ? null
            : $"{_apiBase}/api/public/loja/{t.IntakeSlug}/ifthenpay/callback?key=[ANTI_PHISHING_KEY]&id=[ID]&amount=[AMOUNT]&payment_datetime=[PAYMENT_DATETIME]&payment_method=[PAYMENT_METHOD]",
        // Só os códigos (nunca as chaves) voltam ao browser.
        MetodosIfthenpay.Todos.Select(m => m.Codigo)
            .Where(MetodosIfthenpay.Ler(t.IfthenpayContasCipherText is { } c ? _secrets.Unprotect(c) : null).ContainsKey).ToList());

    public async Task<PagamentosLojaEstadoDto> EstadoAsync(CancellationToken ct = default) => Dto(await Atual(ct));

    public async Task<PagamentosLojaEstadoDto> GuardarAsync(GuardarPagamentosLojaRequest req, CancellationToken ct = default)
    {
        var t = await Atual(ct);
        if (req.Remover)
        {
            t.IfthenpayGatewayKeyCipherText = null;
            t.IfthenpayAntiPhishingKeyCipherText = null;
            t.IfthenpayContasCipherText = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.GatewayKey)) t.IfthenpayGatewayKeyCipherText = _secrets.Protect(Chave(req.GatewayKey, "Gateway Key"));
            if (!string.IsNullOrWhiteSpace(req.AntiPhishingKey)) t.IfthenpayAntiPhishingKeyCipherText = _secrets.Protect(Chave(req.AntiPhishingKey, "chave anti-phishing"));
            if (req.Metodos is { Count: > 0 })
            {
                var contas = MetodosIfthenpay.Ler(t.IfthenpayContasCipherText is { } c ? _secrets.Unprotect(c) : null);
                foreach (var (codigo, valor) in req.Metodos)
                {
                    var m = MetodosIfthenpay.Todos.FirstOrDefault(x => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
                    if (m.Codigo is null) throw new ValidationException("metodo_invalido", $"Método desconhecido: {codigo}.");
                    if (string.IsNullOrWhiteSpace(valor)) continue;
                    if (valor.Trim() == "-") { contas.Remove(m.Codigo); continue; }
                    var chave = Chave(valor, $"chave {m.Nome}");
                    if (chave.Contains('|') || chave.Contains(';'))
                        throw new ValidationException("chave_invalida", $"A chave {m.Nome} não pode ter «|» nem «;» — cola só a chave (ex.: ABC-123456).");
                    contas[m.Codigo] = chave;
                }
                var s = MetodosIfthenpay.Escrever(contas);
                t.IfthenpayContasCipherText = s is null ? null : _secrets.Protect(s);
            }
        }
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, "PagamentosLoja", t.Id,
            new { gateway = req.GatewayKey is not null, antiPhishing = req.AntiPhishingKey is not null, req.Remover,
                metodos = req.Metodos?.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToArray() }, t.Id, _user.UserId, ct);
        return Dto(t);
    }

    private static string Chave(string v, string nome)
    {
        v = v.Trim();
        if (v.Length is < 4 or > 50 || v.Any(char.IsWhiteSpace))
            throw new ValidationException("chave_invalida", $"A {nome} não parece válida (entre 4 e 50 caracteres, sem espaços).");
        return v;
    }
}
