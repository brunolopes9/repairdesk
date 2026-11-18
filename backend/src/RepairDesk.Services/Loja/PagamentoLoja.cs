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

    private async Task<string?> GatewayKeyAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } id) return null;
        var t = await _tenants.FindByIdAsync(id, ct);
        return t?.IfthenpayGatewayKeyCipherText is { } c && t.IfthenpayAntiPhishingKeyCipherText is not null ? _secrets.Unprotect(c) : null;
    }

    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
        => await GatewayKeyAsync(ct) is not null || _stripe.Configurado;

    public async Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default)
        => await GatewayKeyAsync(ct) is { } key
            ? await _ifthenpay.CriarSessaoAsync(key, pedido, ct)
            : await _stripe.CriarSessaoAsync(pedido, ct);
}

public sealed record PagamentosLojaEstadoDto(bool GatewayKeyDefinida, bool AntiPhishingDefinida, bool Ativo, string? CallbackUrl);
public sealed record GuardarPagamentosLojaRequest(string? GatewayKey, string? AntiPhishingKey, bool Remover = false);

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
            : $"{_apiBase}/api/public/loja/{t.IntakeSlug}/ifthenpay/callback?key=[ANTI_PHISHING_KEY]&id=[ID]&amount=[AMOUNT]&payment_datetime=[PAYMENT_DATETIME]&payment_method=[PAYMENT_METHOD]");

    public async Task<PagamentosLojaEstadoDto> EstadoAsync(CancellationToken ct = default) => Dto(await Atual(ct));

    public async Task<PagamentosLojaEstadoDto> GuardarAsync(GuardarPagamentosLojaRequest req, CancellationToken ct = default)
    {
        var t = await Atual(ct);
        if (req.Remover)
        {
            t.IfthenpayGatewayKeyCipherText = null;
            t.IfthenpayAntiPhishingKeyCipherText = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.GatewayKey)) t.IfthenpayGatewayKeyCipherText = _secrets.Protect(Chave(req.GatewayKey, "Gateway Key"));
            if (!string.IsNullOrWhiteSpace(req.AntiPhishingKey)) t.IfthenpayAntiPhishingKeyCipherText = _secrets.Protect(Chave(req.AntiPhishingKey, "chave anti-phishing"));
        }
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, "PagamentosLoja", t.Id,
            new { gateway = req.GatewayKey is not null, antiPhishing = req.AntiPhishingKey is not null, req.Remover }, t.Id, _user.UserId, ct);
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
