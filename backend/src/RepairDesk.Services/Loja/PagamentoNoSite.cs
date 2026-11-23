using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

/// <summary>O que o site mostra depois de escolher MB WAY ou Multibanco — tudo na página da loja, sem sair do site.</summary>
public sealed record PagamentoNoSiteDto(string Metodo, decimal Valor, string? Entidade = null, string? Referencia = null,
    DateTime? Validade = null, string? Telefone = null);

/// <summary>Resultado de perguntar ao ifthenpay se um pagamento feito no site já entrou.</summary>
public enum EstadoPagamentoNoSite { Pendente, Pago, Recusado, Expirado }

public interface IPagamentoNoSite
{
    /// <summary>Métodos que se pagam dentro do site nesta empresa: «mbway» e/ou «mb».</summary>
    Task<IReadOnlySet<string>> MetodosAsync(CancellationToken ct = default);

    /// <summary>Pede o pagamento (MB WAY: notificação no telemóvel; MB: gera entidade/referência) e guarda os dados na encomenda.</summary>
    Task<PagamentoNoSiteDto> IniciarAsync(EncomendaLoja e, string metodo, string? telefoneMbWay, CancellationToken ct = default);

    Task<EstadoPagamentoNoSite> EstadoAsync(EncomendaLoja e, CancellationToken ct = default);
}

/// <summary>
/// 23/11 (Bruno: «nem mesmo cartão tem que sair do site»): MB WAY e Multibanco pelas APIs diretas do ifthenpay
/// (<c>/spg/payment/mbway</c> e <c>/multibanco/reference/init</c>) — o cliente nunca sai da loja. A confirmação não
/// depende do callback: pergunta-se ao ifthenpay (<c>/mbway/status</c>, <c>/multibanco/reference/list</c>); o callback,
/// quando chega, confirma na mesma (a confirmação é idempotente).
/// </summary>
public sealed class PagamentoNoSiteIfthenpay : IPagamentoNoSite
{
    public const string UrlMbWay = "https://api.ifthenpay.com/spg/payment/mbway";
    public const string UrlMultibanco = "https://api.ifthenpay.com/multibanco/reference";
    public const int DiasMultibanco = 3;

    private readonly HttpClient _http;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly ILogger<PagamentoNoSiteIfthenpay> _log;

    public PagamentoNoSiteIfthenpay(HttpClient http, ITenantRepository tenants, ITenantContext tenant, ISecretProtector secrets,
        ILogger<PagamentoNoSiteIfthenpay> log)
    {
        _http = http;
        _tenants = tenants;
        _tenant = tenant;
        _secrets = secrets;
        _log = log;
    }

    private async Task<Dictionary<string, string>> ChavesAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } id || await _tenants.FindByIdAsync(id, ct) is not { } t
            || t.IfthenpayContasCipherText is not { } c || t.IfthenpayAntiPhishingKeyCipherText is null)
            return new(StringComparer.OrdinalIgnoreCase);
        return MetodosIfthenpay.Ler(_secrets.Unprotect(c));
    }

    public async Task<IReadOnlySet<string>> MetodosAsync(CancellationToken ct = default)
    {
        var k = await ChavesAsync(ct);
        var s = new HashSet<string>();
        if (k.ContainsKey("MBWAY")) s.Add("mbway");
        if (k.ContainsKey("MB")) s.Add("mb");
        return s;
    }

    /// <summary>«912 345 678», «+351912345678», «00351 912…» → «351#912345678». Só telemóveis portugueses (9x).</summary>
    public static string? TelefoneMbWay(string? t)
    {
        var d = new string((t ?? "").Where(char.IsDigit).ToArray());
        if (d.StartsWith("00351")) d = d[5..];
        else if (d.StartsWith("351") && d.Length == 12) d = d[3..];
        return d.Length == 9 && d[0] == '9' ? "351#" + d : null;
    }

    public async Task<PagamentoNoSiteDto> IniciarAsync(EncomendaLoja e, string metodo, string? telefoneMbWay, CancellationToken ct = default)
    {
        var k = await ChavesAsync(ct);
        var valor = (e.TotalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        if (metodo == "mbway")
        {
            if (!k.TryGetValue("MBWAY", out var chave))
                throw new ValidationException("metodo_indisponivel", "O MB WAY não está disponível agora. Escolhe outro método.");
            var tel = TelefoneMbWay(string.IsNullOrWhiteSpace(telefoneMbWay) ? e.Telefone : telefoneMbWay)
                ?? throw new ValidationException("telefone_mbway", "Escreve o número de telemóvel associado ao MB WAY (9 dígitos, começa por 9).");
            using var doc = await Post(UrlMbWay, new Dictionary<string, object>
            {
                ["mbWayKey"] = chave, ["orderId"] = e.Referencia, ["amount"] = valor, ["mobileNumber"] = tel,
                ["email"] = e.Email.Length > 100 ? e.Email[..100] : e.Email, ["description"] = $"Encomenda {e.Referencia} LopesTech",
            }, e.Referencia, ct);
            var status = Texto(doc.RootElement, "Status");
            if (status != "000")
            {
                _log.LogWarning("MB WAY recusou o pedido da encomenda {Ref}: {Status} {Msg}", e.Referencia, status, Texto(doc.RootElement, "Message"));
                throw new ValidationException("mbway_recusado", status == "122"
                    ? "O MB WAY recusou este número. Confirma que o MB WAY está ativo neste telemóvel ou escolhe Multibanco."
                    : "O MB WAY não respondeu. Tenta outra vez ou escolhe Multibanco.");
            }
            e.MetodoPagamento = "mbway";
            e.PagamentoPedidoId = Texto(doc.RootElement, "RequestId");
            return new PagamentoNoSiteDto("mbway", e.TotalCents / 100m, Telefone: tel[4..]);
        }
        if (metodo == "mb")
        {
            if (!k.TryGetValue("MB", out var chave))
                throw new ValidationException("metodo_indisponivel", "O Multibanco não está disponível agora. Escolhe outro método.");
            using var doc = await Post(UrlMultibanco + "/init", new Dictionary<string, object>
            {
                ["mbKey"] = chave, ["orderId"] = e.Referencia, ["amount"] = valor, ["description"] = $"Encomenda {e.Referencia} LopesTech",
                ["clientName"] = e.Nome, ["clientEmail"] = e.Email, ["expiryDays"] = DiasMultibanco,
            }, e.Referencia, ct);
            if (Texto(doc.RootElement, "Status") != "0")
            {
                _log.LogWarning("Multibanco recusou a referência da encomenda {Ref}: {Msg}", e.Referencia, Texto(doc.RootElement, "Message"));
                throw new ValidationException("mb_recusado", "Não foi possível gerar a referência Multibanco. Tenta outra vez ou escolhe MB WAY.");
            }
            e.MetodoPagamento = "mb";
            e.PagamentoPedidoId = Texto(doc.RootElement, "RequestId");
            e.MbEntidade = Texto(doc.RootElement, "Entity");
            e.MbReferencia = Texto(doc.RootElement, "Reference");
            e.MbValidade = DateTime.TryParseExact(Texto(doc.RootElement, "ExpiryDate"), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                ? v.Date.AddHours(23).AddMinutes(59) : DateTime.UtcNow.Date.AddDays(DiasMultibanco).AddHours(23).AddMinutes(59);
            return Dto(e)!;
        }
        throw new ValidationException("metodo_invalido", "Método de pagamento desconhecido.");
    }

    /// <summary>O que a página da encomenda volta a mostrar (ex.: a referência Multibanco depois de recarregar).</summary>
    public static PagamentoNoSiteDto? Dto(EncomendaLoja e) => e.MetodoPagamento switch
    {
        "mb" => new PagamentoNoSiteDto("mb", e.TotalCents / 100m, e.MbEntidade, FormatarReferencia(e.MbReferencia), e.MbValidade),
        "mbway" => new PagamentoNoSiteDto("mbway", e.TotalCents / 100m),
        _ => null,
    };

    /// <summary>«544241017» → «544 241 017» (como no multibanco).</summary>
    public static string? FormatarReferencia(string? r)
        => r is { Length: 9 } && r.All(char.IsDigit) ? $"{r[..3]} {r[3..6]} {r[6..]}" : r;

    public async Task<EstadoPagamentoNoSite> EstadoAsync(EncomendaLoja e, CancellationToken ct = default)
    {
        var k = await ChavesAsync(ct);
        try
        {
            if (e.MetodoPagamento == "mbway" && e.PagamentoPedidoId is { } req && k.TryGetValue("MBWAY", out var chaveW))
            {
                using var resp = await _http.GetAsync($"{UrlMbWay}/status?mbWayKey={Uri.EscapeDataString(chaveW)}&requestId={Uri.EscapeDataString(req)}", ct);
                if (!resp.IsSuccessStatusCode) return EstadoPagamentoNoSite.Pendente;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                return EstadoMbWay(Texto(doc.RootElement, "Status"));
            }
            if (e.MetodoPagamento == "mb" && k.TryGetValue("MB", out var chaveM))
            {
                using var doc = await Post(UrlMultibanco + "/list", new Dictionary<string, object>
                {
                    ["mbKey"] = chaveM,
                    ["fromDate"] = e.CreatedAt.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["toDate"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["orderId"] = e.Referencia,
                }, e.Referencia, ct);
                return EstadoMultibanco(doc.RootElement, e.TotalCents, e.MbValidade, DateTime.UtcNow);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or DomainException)
        {
            _log.LogInformation(ex, "Estado do pagamento da encomenda {Ref} indisponível agora.", e.Referencia);
        }
        return EstadoPagamentoNoSite.Pendente;
    }

    /// <summary>Códigos do <c>/mbway/status</c>: 000 pago, 020 recusado pelo cliente, 101 expirou (4 min), 122 recusado.</summary>
    public static EstadoPagamentoNoSite EstadoMbWay(string? status) => status switch
    {
        "000" => EstadoPagamentoNoSite.Pago,
        "020" or "122" => EstadoPagamentoNoSite.Recusado,
        "101" => EstadoPagamentoNoSite.Expirado,
        _ => EstadoPagamentoNoSite.Pendente,
    };

    /// <summary>Resposta do <c>/multibanco/reference/list</c>: paga só com «PAID» e o valor certo; sem isso, expira na validade.</summary>
    public static EstadoPagamentoNoSite EstadoMultibanco(JsonElement lista, long totalCents, DateTime? validade, DateTime agora)
    {
        if (lista.ValueKind == JsonValueKind.Array)
            foreach (var item in lista.EnumerateArray())
                if (string.Equals(Texto(item, "status"), "PAID", StringComparison.OrdinalIgnoreCase)
                    && IfthenpayLojaCheckout.ValorBate(Texto(item, "amount"), totalCents))
                    return EstadoPagamentoNoSite.Pago;
        return validade is { } v && v < agora ? EstadoPagamentoNoSite.Expirado : EstadoPagamentoNoSite.Pendente;
    }

    private async Task<JsonDocument> Post(string url, Dictionary<string, object> corpo, string referencia, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try { resp = await _http.PostAsJsonAsync(url, corpo, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(ex, "ifthenpay indisponível (encomenda {Ref})", referencia);
            throw new ValidationException("pagamento_indisponivel", "O serviço de pagamento não respondeu. Tenta outra vez dentro de momentos.");
        }
        using (resp)
        {
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("ifthenpay HTTP {Status} para a encomenda {Ref}: {Corpo}", (int)resp.StatusCode, referencia, texto.Length > 300 ? texto[..300] : texto);
                throw new ValidationException("pagamento_recusado", "O serviço de pagamento recusou o pedido. Tenta outra vez ou fala connosco pelo WhatsApp 920 182 332.");
            }
            try { return JsonDocument.Parse(texto); }
            catch (JsonException)
            {
                throw new ValidationException("pagamento_recusado", "Resposta inesperada do serviço de pagamento. Tenta outra vez.");
            }
        }
    }

    public static string? Texto(JsonElement o, string nome)
    {
        if (o.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in o.EnumerateObject())
            if (string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.GetRawText(),
                    _ => null,
                };
        return null;
    }
}
