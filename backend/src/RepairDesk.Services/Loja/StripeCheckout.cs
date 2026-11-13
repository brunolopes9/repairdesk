using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

/// <summary>Sprint 656: configuração do Stripe (secção «Stripe» — as chaves vivem só no servidor, nunca no git).</summary>
public sealed class StripeOptions
{
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
    /// <summary>Endereço do site para onde o Stripe devolve o cliente.</summary>
    public string SiteBase { get; set; } = "https://lopestech.pt";
}

public sealed record StripeLinha(string Nome, long PrecoUnitarioCents, int Quantidade);
public sealed record StripeSessaoPedido(Guid EncomendaId, Guid TenantId, string Referencia, string Email,
    IReadOnlyList<StripeLinha> Linhas, string UrlSucesso, string UrlCancelar);
public sealed record StripeSessao(string Id, string Url);

/// <summary>Sprint 656: o que o Mender precisa do Stripe — criar a página de pagamento (Checkout hospedado).</summary>
public interface IStripeCheckout
{
    bool Configurado { get; }
    Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default);
}

/// <summary>
/// Stripe Checkout por HTTP (sem SDK). Os dados do cartão nunca passam pelo Mender: o cliente paga na página do
/// Stripe (cartão, MB WAY, Multibanco — os métodos ativos no painel do Stripe) e o webhook confirma.
/// </summary>
public sealed class StripeCheckoutClient : IStripeCheckout
{
    private readonly HttpClient _http;
    private readonly StripeOptions _opt;
    private readonly ILogger<StripeCheckoutClient> _log;

    public StripeCheckoutClient(HttpClient http, IOptions<StripeOptions> opt, ILogger<StripeCheckoutClient> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_opt.SecretKey) && !string.IsNullOrWhiteSpace(_opt.WebhookSecret);

    public async Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido p, CancellationToken ct = default)
    {
        if (!Configurado) throw new ValidationException("pagamentos_inativos", "Os pagamentos online ainda não estão ativos.");
        var campos = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("locale", "pt"),
            new("customer_email", p.Email),
            new("client_reference_id", p.EncomendaId.ToString()),
            new("success_url", p.UrlSucesso),
            new("cancel_url", p.UrlCancelar),
            new("metadata[encomenda_id]", p.EncomendaId.ToString()),
            new("metadata[tenant_id]", p.TenantId.ToString()),
            new("metadata[referencia]", p.Referencia),
            new("payment_intent_data[description]", $"Encomenda {p.Referencia} — LopesTech"),
        };
        for (var i = 0; i < p.Linhas.Count; i++)
        {
            var l = p.Linhas[i];
            campos.Add(new($"line_items[{i}][quantity]", l.Quantidade.ToString()));
            campos.Add(new($"line_items[{i}][price_data][currency]", "eur"));
            campos.Add(new($"line_items[{i}][price_data][unit_amount]", l.PrecoUnitarioCents.ToString()));
            campos.Add(new($"line_items[{i}][price_data][product_data][name]", l.Nome));
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(campos),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.SecretKey);
        // A mesma encomenda nunca cria duas sessões (duplo clique, nova tentativa de rede).
        req.Headers.Add("Idempotency-Key", $"encomenda-{p.EncomendaId:N}");

        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "Stripe indisponível ao criar a sessão da encomenda {Ref}", p.Referencia);
            throw new ValidationException("stripe_indisponivel", "O serviço de pagamentos não respondeu. Tenta outra vez daqui a pouco.");
        }
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Stripe recusou a sessão da encomenda {Ref}: {Status} {Corpo}", p.Referencia, (int)resp.StatusCode,
                corpo.Length > 500 ? corpo[..500] : corpo);
            throw new ValidationException("stripe_erro", "Não foi possível abrir a página de pagamento. Tenta outra vez.");
        }
        using var doc = JsonDocument.Parse(corpo);
        var id = doc.RootElement.GetProperty("id").GetString();
        var url = doc.RootElement.GetProperty("url").GetString();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url))
            throw new ValidationException("stripe_erro", "Resposta inesperada do serviço de pagamentos.");
        return new StripeSessao(id, url);
    }
}

/// <summary>Sprint 656: assinatura dos webhooks do Stripe (cabeçalho «Stripe-Signature»: t=…,v1=…).</summary>
public static class StripeWebhook
{
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(5);

    public static bool AssinaturaValida(string payload, string? cabecalho, string? segredo, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(cabecalho) || string.IsNullOrWhiteSpace(segredo)) return false;
        long? t = null;
        var assinaturas = new List<string>();
        foreach (var parte in cabecalho.Split(','))
        {
            var kv = parte.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "t" && long.TryParse(kv[1], out var ts)) t = ts;
            else if (kv[0].Trim() == "v1") assinaturas.Add(kv[1].Trim());
        }
        if (t is null || assinaturas.Count == 0) return false;
        if ((agora - DateTimeOffset.FromUnixTimeSeconds(t.Value)).Duration() > Tolerancia) return false;

        var esperado = Assinar(payload, t.Value, segredo);
        var esperadoBytes = Encoding.ASCII.GetBytes(esperado);
        return assinaturas.Any(a => CryptographicOperations.FixedTimeEquals(esperadoBytes, Encoding.ASCII.GetBytes(a)));
    }

    public static string Assinar(string payload, long timestamp, string segredo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
    }
}
