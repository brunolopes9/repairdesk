using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RepairDesk.Services.Loja;

public interface IFichaFornecedor
{
    /// <summary>
    /// Ficha técnica oficial do produto no site do fornecedor (título + texto), procurada pelos códigos dados
    /// (SKU do lote, modelo). Null quando não encontra ou o site não responde — nunca falha o pedido.
    /// </summary>
    Task<string?> ProcurarAsync(IEnumerable<string> codigos, CancellationToken ct = default);

    /// <summary>23/11: a ficha completa — texto, marca (vendor) e as fotos oficiais do produto. Null quando não encontra.</summary>
    Task<FichaProduto?> ProcurarProdutoAsync(IEnumerable<string> codigos, CancellationToken ct = default);
}

/// <summary>Produto encontrado no site do fornecedor. <see cref="Imagens"/> são URLs https absolutos, pela ordem do fornecedor.</summary>
public sealed record FichaProduto(string Titulo, string Texto, string? Marca, IReadOnlyList<string> Imagens)
{
    public string TextoCompleto => (Titulo + "\n" + Texto).Trim();
}

/// <summary>
/// Sprint 689: a IA da loja só pode usar factos que recebe — sem a ficha do fornecedor ficava sem watts, mAh,
/// comprimentos… Vai buscar a ficha pública à loja Shopify do fornecedor (4Phones: /search/suggest.json e
/// /products/{handle}.js), pelo SKU que veio na fatura ou pelo modelo.
/// </summary>
public sealed partial class FichaFornecedor : IFichaFornecedor
{
    private static readonly string[] Lojas = ["https://4phones.eu"];
    private readonly HttpClient _http;
    private readonly ILogger<FichaFornecedor> _logger;

    public FichaFornecedor(HttpClient http, ILogger<FichaFornecedor> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> ProcurarAsync(IEnumerable<string> codigos, CancellationToken ct = default)
        => (await ProcurarProdutoAsync(codigos, ct))?.TextoCompleto;

    public async Task<FichaProduto?> ProcurarProdutoAsync(IEnumerable<string> codigos, CancellationToken ct = default)
    {
        var lista = codigos.Select(c => c?.Trim() ?? "").Where(c => c.Length is >= 4 and <= 40 && CodigoValido().IsMatch(c))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        foreach (var loja in Lojas)
            foreach (var codigo in lista)
            {
                try
                {
                    var sugestao = await GetJson($"{loja}/search/suggest.json?q={Uri.EscapeDataString(codigo)}&resources%5Btype%5D=product&resources%5Blimit%5D=1", ct);
                    if (sugestao is null || !sugestao.Value.TryGetProperty("resources", out var r)
                        || !r.TryGetProperty("results", out var res) || !res.TryGetProperty("products", out var ps)
                        || ps.ValueKind != JsonValueKind.Array || ps.GetArrayLength() == 0) continue;
                    var handle = ps[0].GetProperty("handle").GetString();
                    if (string.IsNullOrWhiteSpace(handle)) continue;
                    var produto = await GetJson($"{loja}/products/{Uri.EscapeDataString(handle)}.js", ct);
                    if (produto is null) continue;
                    return LerProduto(produto.Value);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    _logger.LogInformation(ex, "Ficha do fornecedor {Loja} para {Codigo} indisponível.", loja, codigo);
                }
            }
        return null;
    }

    /// <summary>Produto Shopify (/products/{handle}.js) → ficha. As imagens vêm como «//cdn…» — passam a https.</summary>
    public static FichaProduto LerProduto(JsonElement produto)
    {
        var titulo = produto.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var html = produto.TryGetProperty("description", out var d) ? d.GetString() : null;
        var marca = produto.TryGetProperty("vendor", out var v) ? v.GetString()?.Trim() : null;
        var imagens = new List<string>();
        if (produto.TryGetProperty("images", out var im) && im.ValueKind == JsonValueKind.Array)
            foreach (var i in im.EnumerateArray())
            {
                var url = i.ValueKind == JsonValueKind.String ? i.GetString()
                    : i.ValueKind == JsonValueKind.Object && i.TryGetProperty("src", out var src) ? src.GetString() : null;
                if (string.IsNullOrWhiteSpace(url)) continue;
                if (url.StartsWith("//")) url = "https:" + url;
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !imagens.Contains(url)) imagens.Add(url);
            }
        return new FichaProduto(titulo, Limpar(html), string.IsNullOrWhiteSpace(marca) ? null : marca, imagens);
    }

    private async Task<JsonElement?> GetJson(string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; LopesTechLoja/1.0)");
        using var resp = await _http.SendAsync(req, cts.Token);
        if (!resp.IsSuccessStatusCode) return null;
        var corpo = await resp.Content.ReadAsStringAsync(cts.Token);
        return JsonDocument.Parse(corpo).RootElement.Clone();
    }

    /// <summary>HTML da descrição → texto simples (máx. 2000 caracteres).</summary>
    public static string Limpar(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var s = Regex.Replace(html, @"<(br|/p|/li|/tr|/h\d)\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Tags().Replace(s, " ");
        s = WebUtility.HtmlDecode(s).Replace('，', ',').Replace('：', ':');
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @"\s*\n\s*", "\n").Trim();
        return s.Length > 2000 ? s[..2000] : s;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9 ._-]*$")]
    private static partial Regex CodigoValido();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
