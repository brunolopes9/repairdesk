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
    /// <paramref name="nomes"/>: nomes originais das linhas da fatura (em inglês, como no site) — usados quando
    /// os códigos não dão nada; só aceita um produto com o mesmo modelo (números e Pro/Max/Plus/… iguais).
    Task<FichaProduto?> ProcurarProdutoAsync(IEnumerable<string> codigos, CancellationToken ct = default, IEnumerable<string>? nomes = null);
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

    public async Task<FichaProduto?> ProcurarProdutoAsync(IEnumerable<string> codigos, CancellationToken ct = default, IEnumerable<string>? nomes = null)
    {
        var lista = codigos.Select(c => c?.Trim() ?? "").Where(c => c.Length is >= 4 and <= 40 && CodigoValido().IsMatch(c))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        var porNome = (nomes ?? []).Select(n => n?.Trim() ?? "").Where(n => n.Length is >= 8 and <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        foreach (var loja in Lojas)
        {
            foreach (var codigo in lista)
                if (await Tentar(loja, codigo, 1, null, ct) is { } f) return f;
            // 23/11: o 4Phones não pesquisa por SKU — procura pelo nome da fatura e confirma que é o mesmo modelo.
            foreach (var nome in porNome)
                if (await Tentar(loja, nome, 6, nome, ct) is { } f) return f;
        }
        // 23/11: Tudo4Mobile (faturas em papel) — o site pesquisa pelo código da fatura.
        foreach (var codigo in lista)
            if (await TentarTudo4Mobile(codigo, porNome, ct) is { } f) return f;
        return null;
    }

    private const string Tudo4Mobile = "https://www.tudo4mobile.pt";

    /// <summary>
    /// Loja OpenCart da Tudo4Mobile: pesquisa pelo código, abre os 3 primeiros resultados e aceita o produto cujo
    /// SKU (dados estruturados da página) é o código da fatura — ou, sem SKU igual, o que tem o mesmo modelo.
    /// </summary>
    private async Task<FichaProduto?> TentarTudo4Mobile(string codigo, IReadOnlyList<string> nomes, CancellationToken ct)
    {
        try
        {
            var pesquisa = await GetTexto($"{Tudo4Mobile}/index.php?route=product/search&search={Uri.EscapeDataString(codigo)}", ct);
            if (pesquisa is null) return null;
            var ids = ProdutoIdT4m().Matches(pesquisa).Select(m => m.Groups[1].Value).Distinct().Take(3).ToList();
            foreach (var id in ids)
            {
                var pagina = await GetTexto($"{Tudo4Mobile}/index.php?route=product/product&product_id={id}", ct);
                if (pagina is null) continue;
                var ficha = LerPaginaTudo4Mobile(pagina, out var sku);
                if (ficha is null) continue;
                if (string.Equals(sku, codigo, StringComparison.OrdinalIgnoreCase)) return ficha;
                if (nomes.Any(n => Semelhanca(n, ficha.Titulo) >= 0.25)) return ficha;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogInformation(ex, "Tudo4Mobile indisponível para {Codigo}.", codigo);
        }
        return null;
    }

    /// <summary>Página de produto da Tudo4Mobile → ficha (JSON-LD «Product» + fotos 1000×1000 da galeria).</summary>
    public static FichaProduto? LerPaginaTudo4Mobile(string html, out string? sku)
    {
        sku = null;
        string? nome = null, descricao = null, marca = null;
        foreach (Match m in JsonLd().Matches(html))
        {
            try
            {
                using var doc = JsonDocument.Parse(m.Groups[1].Value);
                var r = doc.RootElement;
                if (!r.TryGetProperty("@type", out var t) || t.GetString() != "Product") continue;
                nome ??= r.TryGetProperty("name", out var n) ? WebUtility.HtmlDecode(n.GetString()) : null;
                sku ??= r.TryGetProperty("sku", out var k) ? k.GetString() : null;
                if (r.TryGetProperty("description", out var d) && (d.GetString()?.Length ?? 0) > (descricao?.Length ?? 0))
                    descricao = WebUtility.HtmlDecode(d.GetString());
                if (marca is null && r.TryGetProperty("brand", out var b) && b.ValueKind == JsonValueKind.Object
                    && b.TryGetProperty("name", out var bn) && bn.GetString() is { } bns && !bns.Equals("UNIVERSAL", StringComparison.OrdinalIgnoreCase))
                    marca = bns;
            }
            catch (JsonException)
            {
            }
        }
        if (string.IsNullOrWhiteSpace(nome)) return null;
        var imagens = FotoT4m().Matches(html).Select(m => m.Value.Replace(" ", "%20"))
            .Where(u => !Regex.IsMatch(u, "logo|fav|/Ivan/|catt|banner", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
        return new FichaProduto(nome.Trim(), Limpar(descricao), marca, imagens);
    }

    private async Task<string?> GetTexto(string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; LopesTechLoja/1.0)");
        using var resp = await _http.SendAsync(req, cts.Token);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(cts.Token) : null;
    }

    [GeneratedRegex(@"route=product/product&(?:amp;)?product_id=(\d+)")]
    private static partial Regex ProdutoIdT4m();

    [GeneratedRegex(@"<script type=""application/ld\+json"">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex JsonLd();

    [GeneratedRegex(@"https://www\.tudo4mobile\.pt/image/cache/catalog/[^""'<>]+?-1000x1000\.(?:jpe?g|png|webp)", RegexOptions.IgnoreCase)]
    private static partial Regex FotoT4m();

    private async Task<FichaProduto?> Tentar(string loja, string q, int limite, string? confirmarNome, CancellationToken ct)
    {
        try
        {
            var sugestao = await GetJson($"{loja}/search/suggest.json?q={Uri.EscapeDataString(q)}&resources%5Btype%5D=product&resources%5Blimit%5D={limite}", ct);
            if (sugestao is null || !sugestao.Value.TryGetProperty("resources", out var r)
                || !r.TryGetProperty("results", out var res) || !res.TryGetProperty("products", out var ps)
                || ps.ValueKind != JsonValueKind.Array) return null;
            string? handle = null;
            var melhor = 0.0;
            foreach (var p in ps.EnumerateArray())
            {
                var h = p.TryGetProperty("handle", out var hh) ? hh.GetString() : null;
                if (string.IsNullOrWhiteSpace(h)) continue;
                if (confirmarNome is null) { handle = h; break; }
                var titulo = p.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var nota = Semelhanca(confirmarNome, titulo);
                if (nota > melhor) { melhor = nota; handle = h; }
            }
            if (handle is null || (confirmarNome is not null && melhor < 0.6)) return null;
            var produto = await GetJson($"{loja}/products/{Uri.EscapeDataString(handle)}.js", ct);
            return produto is null ? null : LerProduto(produto.Value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogInformation(ex, "Ficha do fornecedor {Loja} para {Q} indisponível.", loja, q);
            return null;
        }
    }

    private static readonly HashSet<string> Variantes = new(StringComparer.OrdinalIgnoreCase)
        { "pro", "max", "plus", "ultra", "mini", "lite", "fe", "air", "neo", "prime" };
    private static readonly HashSet<string> Vazias = new(StringComparer.OrdinalIgnoreCase)
        { "for", "the", "with", "and", "de", "da", "do", "para", "com", "e", "a", "apple" };

    /// <summary>
    /// 0–1: quanto o título do site se parece com o nome da fatura. 0 quando o modelo não é o mesmo — os números
    /// e as variantes (Pro, Max, Plus, Ultra…) têm de ser exatamente os mesmos, senão vinham fotos do telemóvel errado.
    /// </summary>
    public static double Semelhanca(string nomeFatura, string tituloSite)
    {
        static List<string> Tokens(string s) => Regex.Matches(s.ToLowerInvariant(), @"[a-z0-9]+").Select(m => m.Value)
            .Where(t => !Vazias.Contains(t)).ToList();
        var a = Tokens(nomeFatura);
        var b = Tokens(tituloSite);
        if (a.Count == 0 || b.Count == 0) return 0;
        static HashSet<string> Modelo(List<string> t) => t.Where(x => Variantes.Contains(x) || x.Any(char.IsDigit)).ToHashSet();
        if (!Modelo(a).SetEquals(Modelo(b))) return 0;
        var sa = a.ToHashSet();
        var sb = b.ToHashSet();
        return (double)sa.Intersect(sb).Count() / sa.Union(sb).Count();
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
