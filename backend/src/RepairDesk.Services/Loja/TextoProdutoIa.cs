using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Loja;

/// <summary>O que a IA propõe para um produto da loja — o dono revê e o formulário grava sozinho.</summary>
public sealed record TextoProdutoGerado(string Descricao, IReadOnlyList<SpecLoja> Specs, string SeoTitulo, string SeoDescricao);

public interface ITextoProdutoIa
{
    Task<TextoProdutoGerado> GerarAsync(GuardarProdutoLojaRequest produto, CancellationToken ct = default);
}

/// <summary>
/// Sprint 672: descrição, especificações e textos do Google de um produto da loja, escritos pelo Claude através do
/// serviço «ia» (subscrição do dono — 0 € de API). Só usa os dados do formulário: nunca inventa números que não lá estão.
/// </summary>
public sealed class TextoProdutoIa : ITextoProdutoIa
{
    private readonly HttpClient _http;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILogger<TextoProdutoIa> _logger;
    private readonly string? _url;
    private readonly string? _secret;
    private readonly IFichaFornecedor? _ficha;
    private readonly RepairDesk.Core.Abstractions.IArtigoRepository? _artigos;

    public TextoProdutoIa(HttpClient http, IConfiguration config, ILlmUsageTracker tracker, ILogger<TextoProdutoIa> logger,
        IFichaFornecedor? ficha = null, RepairDesk.Core.Abstractions.IArtigoRepository? artigos = null)
    {
        _ficha = ficha;
        _artigos = artigos;
        _http = http;
        _tracker = tracker;
        _logger = logger;
        _url = config["Ia:RunnerUrl"]?.TrimEnd('/');
        _secret = config["Ia:RunnerSecret"];
    }

    private const string Sistema = """
        És o redator da loja online da LopesTech (lopestech.pt), uma loja de reparação de telemóveis em Viseu que também vende
        acessórios, peças e equipamentos recondicionados, com envio CTT para todo o país no dia útil seguinte.
        Escreves em português de Portugal (PT-PT: «ecrã», «telemóvel», «carregar», nunca português do Brasil), claro e honesto,
        para clientes normais — sem jargão de vendas, sem exageros, sem emojis, sem «revolucionário», «incrível», «premium» vazio.
        Regras:
        - Usa só os factos que recebes. Nunca inventes números (mAh, W, resolução, materiais, compatibilidades) que não estejam nos dados.
          Se um dado útil faltar, não o menciones.
        - Quando vier a «Ficha técnica oficial do fornecedor» (normalmente em inglês), traduz e usa TODOS os números técnicos
          dela nas specs: potência (W) total e por porta, entrada/saída (V/A), protocolos (PD, QC), capacidade (mAh), comprimento,
          velocidade de dados, material, dimensões, peso, compatibilidades. Ex.: «USB-C (Power Delivery)»: «5 V 3 A · 9 V 2,22 A — até 20 W».
          Na descrição explica o que esses números significam para o cliente (ex.: «carregamento rápido no iPhone»), sem exagerar.
          Não afirmes compatibilidades de carregamento rápido de marcas que a ficha não indica.
        - Peças de substituição: diz para que modelo serve, o que inclui (ex.: com frame), a qualidade indicada no título
          (ex.: Service Pack = peça original da marca; Original Remaded/OEM = qualidade equivalente) e que a LopesTech pode fazer a montagem na loja.
        - Recondicionados/usados: refere o grau estético e a garantia.
        - A garantia legal em Portugal é de 3 anos (36 meses) para bens novos e recondicionados.
        Responde SÓ com JSON válido, sem texto antes ou depois, no formato:
        {"descricao":"2 a 4 parágrafos curtos separados por \n\n (80–160 palavras)",
         "specs":[{"nome":"Compatível com","valor":"..."}],
         "seoTitulo":"até 65 caracteres, com marca e modelo",
         "seoDescricao":"até 160 caracteres, com o benefício principal e «envio para todo o país»"}
        specs: 3 a 10 linhas só com factos dos dados — primeiro os técnicos (potência, portas, capacidade, comprimento…), depois
        compatibilidade, cor, material, o que inclui. Não repitas marca, modelo, estado nem garantia (a página já os mostra).
        """;

    public async Task<TextoProdutoGerado> GerarAsync(GuardarProdutoLojaRequest p, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(p.Titulo))
            throw new ValidationException("sem_titulo", "Escreve primeiro o título do produto.");
        if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_secret))
            throw new ConflictException("ia_indisponivel", "O assistente de IA não está ligado neste servidor.");

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(150));
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/v1/texto")
            {
                Content = JsonContent.Create(new { system = Sistema, prompt = Dados(p, await FichaAsync(p, ct)), model = "opus" }),
            };
            req.Headers.Add("X-Ia-Secret", _secret);
            using var resp = await _http.SendAsync(req, cts.Token);
            var corpo = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                outcome = corpo.Contains("sem_token", StringComparison.Ordinal) ? "sem_token" : "error";
                _logger.LogWarning("Serviço ia {Status}: {Body}", (int)resp.StatusCode, corpo.Length > 300 ? corpo[..300] : corpo);
                throw new ConflictException("ia_falhou", outcome == "sem_token"
                    ? "Falta ligar a subscrição do Claude ao servidor."
                    : "O assistente não respondeu. Tenta outra vez daqui a um minuto.");
            }
            var texto = JsonDocument.Parse(corpo).RootElement.GetProperty("text").GetString() ?? "";
            return Ler(texto) ?? throw new ConflictException("ia_resposta_invalida", "O assistente respondeu num formato estranho. Tenta outra vez.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            _logger.LogWarning(ex, "Texto do produto pela subscrição falhou.");
            throw new ConflictException("ia_falhou", "O assistente demorou demasiado. Tenta outra vez.");
        }
        finally
        {
            await _tracker.RecordAsync("subscricao", "loja-texto-produto", new LlmUsageTokens(0, 0, 0, 0), (int)relogio.ElapsedMilliseconds, outcome, CancellationToken.None);
        }
    }

    /// <summary>Sprint 689: ficha oficial do fornecedor pelo SKU dos lotes do artigo e pelo modelo.</summary>
    private async Task<string?> FichaAsync(GuardarProdutoLojaRequest p, CancellationToken ct)
    {
        if (_ficha is null) return null;
        var codigos = new List<string>();
        var nomes = new List<string>();
        if (p.ArtigoId is { } artigoId && _artigos is not null && await _artigos.FindByIdAsync(artigoId, ct) is { } artigo)
        {
            codigos.AddRange(ProdutoAutomatico.Codigos(artigo));
            nomes.AddRange(ProdutoAutomatico.Nomes(artigo));
        }
        if (!string.IsNullOrWhiteSpace(p.Modelo)) codigos.Add(p.Modelo);
        return codigos.Count + nomes.Count == 0 ? null : (await _ficha.ProcurarProdutoAsync(codigos, ct, nomes))?.TextoCompleto;
    }

    private static string Dados(GuardarProdutoLojaRequest p, string? ficha = null)
    {
        var sb = new StringBuilder("Dados do produto:\n");
        void L(string k, object? v) { if (v is not null && !(v is string s && string.IsNullOrWhiteSpace(s))) sb.Append("- ").Append(k).Append(": ").Append(v).Append('\n'); }
        L("Título", p.Titulo);
        L("Marca", p.Marca);
        L("Modelo", p.Modelo);
        L("Categoria", p.Categoria switch
        {
            LojaCategoria.Pecas => "Peça de substituição", LojaCategoria.Acessorios => "Acessório", LojaCategoria.Smartphones => "Smartphone",
            LojaCategoria.Computadores => "Computador", LojaCategoria.Tablets => "Tablet", LojaCategoria.Consolas => "Consola", _ => "Outros",
        });
        L("Estado", p.Estado switch
        {
            LojaEstado.Novo => "Novo", LojaEstado.NovoCaixaAberta => "Novo (caixa aberta)", LojaEstado.Recondicionado => "Recondicionado", _ => "Usado",
        });
        L("Grau estético", p.Grau);
        L("Garantia (meses)", p.GarantiaMeses);
        L("Cor", p.Cor);
        L("RAM (GB)", p.RamGb);
        L("Armazenamento (GB)", p.ArmazenamentoGb);
        L("Preço (€)", p.Preco > 0 ? p.Preco.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : null);
        foreach (var s in p.Specs ?? []) L(s.Nome, s.Valor);
        if (!string.IsNullOrWhiteSpace(ficha)) sb.Append("\nFicha técnica oficial do fornecedor:\n").Append(ficha.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(p.Descricao)) sb.Append("\nDescrição atual (melhora-a, mantém os factos):\n").Append(p.Descricao.Trim()).Append('\n');
        return sb.Append("\nDevolve só o JSON.").ToString();
    }

    public static TextoProdutoGerado? Ler(string texto)
    {
        var i = texto.IndexOf('{');
        var f = texto.LastIndexOf('}');
        if (i < 0 || f <= i) return null;
        try
        {
            var r = JsonDocument.Parse(EscaparQuebras(texto[i..(f + 1)])).RootElement;
            string S(string k, int max) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? Cortar(v.GetString()!.Trim(), max) : "";
            var specs = r.TryGetProperty("specs", out var a) && a.ValueKind == JsonValueKind.Array
                ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
                    .Select(x => new SpecLoja(
                        x.TryGetProperty("nome", out var n) ? Cortar(n.GetString()?.Trim() ?? "", 60) : "",
                        x.TryGetProperty("valor", out var v) ? Cortar(v.GetString()?.Trim() ?? "", 200) : ""))
                    .Where(s => s.Nome.Length > 0 && s.Valor.Length > 0).Take(12).ToList()
                : [];
            var descricao = S("descricao", 4000);
            return descricao.Length == 0 ? null : new TextoProdutoGerado(descricao, specs, S("seoTitulo", 70), S("seoDescricao", 170));
        }
        catch (JsonException) { return null; }
    }

    /// <summary>O modelo às vezes põe quebras de linha cruas dentro das strings — o JSON não as aceita; escapam-se.</summary>
    private static string EscaparQuebras(string json)
    {
        var sb = new StringBuilder(json.Length + 16);
        var dentro = false;
        for (var k = 0; k < json.Length; k++)
        {
            var c = json[k];
            if (dentro && c == '\\' && k + 1 < json.Length) { sb.Append(c).Append(json[++k]); continue; }
            if (c == '"') dentro = !dentro;
            if (dentro && c == '\n') { sb.Append("\\n"); continue; }
            if (dentro && c == '\r') continue;
            if (dentro && c == '\t') { sb.Append("\\t"); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Cortar(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd();
}
