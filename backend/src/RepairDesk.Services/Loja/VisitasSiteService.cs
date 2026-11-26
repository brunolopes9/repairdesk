using System.Text.RegularExpressions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

// ---- Pedido público (js/visitas.js no lopestech.pt) ----------------------------------------------------

/// <summary>Lote de eventos de uma página. Nomes curtos — vai em cada clique (sendBeacon).</summary>
public sealed record EventosSiteRequest(string? V, string? S, bool? C, string? Ecra, string? Idioma, string? Loja,
    IReadOnlyList<EventoSiteRequest>? E);

/// <summary>t=tipo, u=caminho(+query na 1.ª página), ti=título, r=referrer, q=termo, n=resultados, p=produto, ps=slug,
/// val=valor, d=segundos, sc=scroll %, rf=referência da encomenda.</summary>
public sealed record EventoSiteRequest(string? T, string? U, string? Ti, string? R, string? Q, int? N, string? P, string? Ps,
    decimal? Val, int? D, int? Sc, string? Rf);

// ---- Relatório (Mender → Loja online → Visitas) --------------------------------------------------------

public sealed record ContagemDto(string Nome, int Total, int? Extra = null, double? Media = null);
public sealed record DiaVisitasDto(DateOnly Dia, int Visitas, int Visitantes, int Paginas, int Compras);
public sealed record FunilDto(int Visitas, int ViramProduto, int Carrinho, int Checkout, int Pagamento, int Compras, decimal ValorComprado);

public sealed record VisitasResumoDto(
    int Visitantes, int Visitas, int Paginas, int TempoMedioSeg, int VisitasDeUmaPagina, int Regressaram,
    FunilDto Funil, IReadOnlyList<DiaVisitasDto> PorDia,
    IReadOnlyList<ContagemDto> TopPaginas, IReadOnlyList<ContagemDto> Entradas, IReadOnlyList<ContagemDto> Saidas,
    IReadOnlyList<ContagemDto> Pesquisas, IReadOnlyList<ContagemDto> PesquisasSemResultados,
    IReadOnlyList<ContagemDto> Produtos, IReadOnlyList<ContagemDto> Carrinhos, IReadOnlyList<ContagemDto> Origens,
    IReadOnlyList<ContagemDto> Campanhas, IReadOnlyList<ContagemDto> Dispositivos, IReadOnlyList<ContagemDto> Contactos,
    bool Truncado);

public sealed record SessaoVisitaDto(string Sessao, string Visitante, DateTime Inicio, DateTime Fim, int DuracaoSeg, int Paginas,
    string? Origem, string? Campanha, string? Dispositivo, string? Sistema, string? Cliente, string? Entrada, string? Saida,
    IReadOnlyList<string> Pesquisas, IReadOnlyList<string> Produtos, bool Carrinho, bool Checkout, bool Pagamento, bool Comprou,
    decimal? Valor, bool Regressou);

public sealed record EventoVisitaDto(DateTime Em, string Tipo, string? Caminho, string? Titulo, string? Termo, int? Resultados,
    string? Produto, decimal? Valor, int? Duracao, int? Scroll, string? Encomenda, string? Origem, string? Referrer);

public interface IVisitasSiteService
{
    Task<int> RegistarAsync(EventosSiteRequest req, string? userAgent, string? cliente, CancellationToken ct = default);
    Task<VisitasResumoDto> ResumoAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);
    Task<IReadOnlyList<SessaoVisitaDto>> SessoesAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);
    Task<IReadOnlyList<EventoVisitaDto>> SessaoAsync(string sessao, CancellationToken ct = default);
    /// <summary>«Fui eu»: apaga esta visita e todas as outras do mesmo visitante. Devolve quantos eventos saíram.</summary>
    Task<int> NaoContarAsync(string sessao, CancellationToken ct = default);
}

/// <summary>
/// 23/11: medição própria do site. Guarda o que chega (tipos conhecidos, textos cortados, sem IP) e faz as contas na
/// leitura — o volume de uma loja pequena cabe em memória (limite de eventos por relatório, com aviso «Truncado»).
/// </summary>
public sealed partial class VisitasSiteService : IVisitasSiteService
{
    public const int MaxEventosPorPedido = 30;
    public const int MaxEventosRelatorio = 250_000;
    public static readonly HashSet<string> Tipos = new(StringComparer.Ordinal)
        { "pagina", "saida", "pesquisa", "produto", "carrinho", "remover", "checkout", "pagamento", "compra", "contacto" };

    private static readonly TimeZoneInfo Lisboa = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly IVisitasSiteRepository _repo;
    private readonly ITenantContext _tenant;

    public VisitasSiteService(IVisitasSiteRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<int> RegistarAsync(EventosSiteRequest req, string? userAgent, string? cliente, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");
        if (EBot(userAgent)) return 0;
        var visitante = Id(req.V);
        var sessao = Id(req.S);
        if (visitante is null || sessao is null || req.E is null || req.E.Count == 0) return 0;
        var (dispositivo, sistema, navegador) = DoUserAgent(userAgent);
        var agora = DateTime.UtcNow;
        var eventos = new List<EventoSite>();
        foreach (var (e, i) in req.E.Take(MaxEventosPorPedido).Select((e, i) => (e, i)))
        {
            var tipo = (e.T ?? "").Trim().ToLowerInvariant();
            if (!Tipos.Contains(tipo)) continue;
            var (caminho, query) = Partir(e.U);
            var (origem, campanha) = tipo == "pagina" ? Origem(e.R, query) : (null, null);
            eventos.Add(new EventoSite
            {
                TenantId = tenantId,
                CreatedAt = agora.AddMilliseconds(i), // mantém a ordem do lote
                Visitante = visitante,
                Sessao = sessao,
                Tipo = tipo,
                Caminho = Cortar(caminho, 300),
                Titulo = Cortar(e.Ti, 200),
                Referrer = tipo == "pagina" ? Cortar(SoOrigem(e.R), 300) : null,
                Origem = origem,
                Campanha = Cortar(campanha, 100),
                Termo = Cortar(e.Q?.Trim().ToLowerInvariant(), 120),
                Resultados = e.N is >= 0 and < 100_000 ? e.N : null,
                Produto = Cortar(e.P, 200),
                ProdutoSlug = Cortar(e.Ps, 200),
                Valor = e.Val is >= 0 and < 100_000 ? Math.Round(e.Val.Value, 2) : null,
                Duracao = e.D is >= 0 and <= 6 * 3600 ? e.D : null,
                Scroll = e.Sc is >= 0 and <= 100 ? e.Sc : null,
                Encomenda = Cortar(e.Rf, 20),
                Cliente = Cortar(cliente, 120),
                Dispositivo = dispositivo,
                Sistema = sistema,
                Navegador = navegador,
                Ecra = Cortar(req.Ecra, 20),
                Idioma = Cortar(req.Idioma, 12),
                Consentimento = req.C == true,
            });
        }
        if (eventos.Count == 0) return 0;
        await _repo.AdicionarAsync(eventos, ct);
        await _repo.SaveAsync(ct);
        return eventos.Count;
    }

    public async Task<VisitasResumoDto> ResumoAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var (deUtc, ateUtc) = Intervalo(de, ate);
        var eventos = await _repo.DoPeriodoAsync(deUtc, ateUtc, MaxEventosRelatorio, ct);
        return Agregar(eventos, eventos.Count >= MaxEventosRelatorio);
    }

    public async Task<IReadOnlyList<SessaoVisitaDto>> SessoesAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var (deUtc, ateUtc) = Intervalo(de, ate);
        var eventos = await _repo.DoPeriodoAsync(deUtc, ateUtc, MaxEventosRelatorio, ct);
        return Sessoes(eventos).Take(500).ToList();
    }

    public async Task<IReadOnlyList<EventoVisitaDto>> SessaoAsync(string sessao, CancellationToken ct = default)
        => (await _repo.DaSessaoAsync(sessao, ct)).Select(e => new EventoVisitaDto(e.CreatedAt, e.Tipo, e.Caminho, e.Titulo, e.Termo,
            e.Resultados, e.Produto, e.Valor, e.Duracao, e.Scroll, e.Encomenda, e.Origem, e.Referrer)).ToList();

    public async Task<int> NaoContarAsync(string sessao, CancellationToken ct = default)
    {
        var visitante = (await _repo.DaSessaoAsync(sessao, ct)).Select(e => e.Visitante).FirstOrDefault()
            ?? throw new NotFoundException("Visita", sessao);
        return await _repo.ApagarDoVisitanteAsync(visitante, ct);
    }

    // ---- Contas (puras, testadas) ----------------------------------------------------------------------

    public static VisitasResumoDto Agregar(IReadOnlyList<EventoSite> eventos, bool truncado = false)
    {
        var sessoes = Sessoes(eventos).ToList();
        var paginas = eventos.Where(e => e.Tipo == "pagina").ToList();
        var saidas = eventos.Where(e => e.Tipo == "saida" && e.Caminho is not null).ToList();
        var compras = eventos.Where(e => e.Tipo == "compra").ToList();

        var porDia = eventos.GroupBy(e => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(e.CreatedAt, Lisboa)))
            .OrderBy(g => g.Key)
            .Select(g => new DiaVisitasDto(g.Key, g.Select(e => e.Sessao).Distinct().Count(), g.Select(e => e.Visitante).Distinct().Count(),
                g.Count(e => e.Tipo == "pagina"), g.Count(e => e.Tipo == "compra")))
            .ToList();

        // O tempo chega em parcelas (cada vez que o separador fica escondido) — média = tempo total ÷ vezes que a página foi vista.
        var vistasPorPagina = paginas.Where(e => e.Caminho is not null).GroupBy(e => e.Caminho!).ToDictionary(g => g.Key, g => g.Count());
        var tempoPorPagina = saidas.Where(e => e.Duracao is not null).GroupBy(e => e.Caminho!)
            .ToDictionary(g => g.Key, g => (double)g.Sum(e => e.Duracao!.Value) / Math.Max(1, vistasPorPagina.GetValueOrDefault(g.Key, 1)));
        var topPaginas = paginas.Where(e => e.Caminho is not null).GroupBy(e => e.Caminho!)
            .Select(g => new ContagemDto(Titulo(g), g.Count(), g.Select(e => e.Sessao).Distinct().Count(),
                tempoPorPagina.TryGetValue(g.Key, out var t) ? Math.Round(t) : null))
            .OrderByDescending(c => c.Total).Take(50).ToList();

        static List<ContagemDto> Top(IEnumerable<string?> nomes, int n = 30) => nomes.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!).Select(g => new ContagemDto(g.Key, g.Count())).OrderByDescending(c => c.Total).ThenBy(c => c.Nome).Take(n).ToList();

        var pesquisas = eventos.Where(e => e.Tipo == "pesquisa" && e.Termo is not null).ToList();
        var semResultados = pesquisas.Where(e => e.Resultados == 0);

        return new VisitasResumoDto(
            Visitantes: eventos.Select(e => e.Visitante).Distinct().Count(),
            Visitas: sessoes.Count,
            Paginas: paginas.Count,
            TempoMedioSeg: sessoes.Count == 0 ? 0 : (int)Math.Round(sessoes.Average(s => s.DuracaoSeg)),
            VisitasDeUmaPagina: sessoes.Count(s => s.Paginas <= 1),
            Regressaram: sessoes.Count(s => s.Regressou),
            Funil: new FunilDto(sessoes.Count, sessoes.Count(s => s.Produtos.Count > 0), sessoes.Count(s => s.Carrinho),
                sessoes.Count(s => s.Checkout), sessoes.Count(s => s.Pagamento), sessoes.Count(s => s.Comprou),
                compras.Sum(e => e.Valor ?? 0)),
            PorDia: porDia,
            TopPaginas: topPaginas,
            Entradas: Top(sessoes.Select(s => s.Entrada)),
            Saidas: Top(sessoes.Select(s => s.Saida)),
            Pesquisas: Top(pesquisas.Select(e => e.Termo), 50),
            PesquisasSemResultados: Top(semResultados.Select(e => e.Termo), 50),
            Produtos: Top(eventos.Where(e => e.Tipo == "produto").Select(e => e.Produto), 50),
            Carrinhos: Top(eventos.Where(e => e.Tipo == "carrinho").Select(e => e.Produto), 50),
            Origens: Top(sessoes.Select(s => s.Origem ?? "direto")),
            Campanhas: Top(sessoes.Select(s => s.Campanha)),
            Dispositivos: Top(sessoes.Select(s => $"{s.Dispositivo ?? "?"} · {s.Sistema ?? "?"}")),
            Contactos: Top(eventos.Where(e => e.Tipo == "contacto").Select(e => e.Termo)),
            Truncado: truncado);
    }

    private static string Titulo(IGrouping<string, EventoSite> g)
    {
        var t = g.Select(e => e.Titulo).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return t is null ? g.Key : $"{g.Key} — {t}";
    }

    public static IEnumerable<SessaoVisitaDto> Sessoes(IReadOnlyList<EventoSite> eventos)
    {
        // «Regressou» = o visitante já tinha uma visita antes desta (no período).
        var primeiraDoVisitante = eventos.GroupBy(e => e.Visitante).ToDictionary(g => g.Key, g => g.Min(e => e.CreatedAt));
        return eventos.GroupBy(e => e.Sessao).Select(g =>
        {
            var lista = g.OrderBy(e => e.CreatedAt).ToList();
            var paginas = lista.Where(e => e.Tipo == "pagina").ToList();
            var primeira = lista.FirstOrDefault(e => e.Origem is not null);
            var duracaoSaidas = lista.Where(e => e.Tipo == "saida").Sum(e => e.Duracao ?? 0);
            var duracao = Math.Max(duracaoSaidas, (int)(lista[^1].CreatedAt - lista[0].CreatedAt).TotalSeconds);
            var compra = lista.LastOrDefault(e => e.Tipo == "compra");
            return new SessaoVisitaDto(g.Key, lista[0].Visitante, lista[0].CreatedAt, lista[^1].CreatedAt, duracao, paginas.Count,
                primeira?.Origem, primeira?.Campanha, lista[0].Dispositivo, lista[0].Sistema,
                lista.Select(e => e.Cliente).LastOrDefault(c => c is not null),
                paginas.FirstOrDefault()?.Caminho, paginas.LastOrDefault()?.Caminho,
                lista.Where(e => e.Tipo == "pesquisa" && e.Termo is not null).Select(e => e.Termo!).Distinct().ToList(),
                lista.Where(e => e.Tipo == "produto" && e.Produto is not null).Select(e => e.Produto!).Distinct().ToList(),
                lista.Any(e => e.Tipo == "carrinho"), lista.Any(e => e.Tipo == "checkout"), lista.Any(e => e.Tipo == "pagamento"),
                compra is not null, compra?.Valor,
                primeiraDoVisitante[lista[0].Visitante] < lista[0].CreatedAt.AddMinutes(-30));
        }).OrderByDescending(s => s.Inicio);
    }

    // ---- Normalização do pedido -----------------------------------------------------------------------

    public static (string? Origem, string? Campanha) Origem(string? referrer, string? query)
    {
        var q = ParseQuery(query);
        var campanha = q.GetValueOrDefault("utm_campaign");
        if (q.TryGetValue("utm_source", out var src) && !string.IsNullOrWhiteSpace(src))
        {
            var s = src.Trim().ToLowerInvariant();
            var origem = s switch
            {
                "fb" or "facebook" or "facebook.com" or "m.facebook.com" => "facebook",
                "ig" or "instagram" => "instagram",
                _ => Cortar(s, 60),
            };
            var medio = q.GetValueOrDefault("utm_medium")?.ToLowerInvariant();
            return (medio is "cpc" or "paid" or "ads" or "paid_social" ? $"{origem} (anúncio)" : origem, campanha);
        }
        if (q.ContainsKey("gclid")) return ("google (anúncio)", campanha);
        if (q.ContainsKey("fbclid")) return (Host(referrer)?.Contains("instagram") == true ? "instagram" : "facebook", campanha);
        var host = Host(referrer);
        if (host is null) return ("direto", campanha);
        if (host.EndsWith("lopestech.pt")) return (null, campanha); // navegação interna: a origem é a da 1.ª página
        string Nome(string h) => h switch
        {
            _ when h.Contains("google.") => "google",
            _ when h.Contains("bing.") => "bing",
            _ when h.Contains("duckduckgo") => "duckduckgo",
            _ when h.Contains("yahoo.") => "yahoo",
            _ when h.Contains("facebook.") || h == "fb.me" => "facebook",
            _ when h.Contains("instagram.") => "instagram",
            _ when h.Contains("whatsapp") || h == "wa.me" => "whatsapp",
            _ when h.Contains("tiktok.") => "tiktok",
            _ when h.Contains("threads.") => "threads",
            _ when h.Contains("youtube.") => "youtube",
            _ when h.Contains("pai.pt") || h.Contains("paginasamarelas") => "páginas amarelas",
            _ => h.StartsWith("www.") ? h[4..] : h,
        };
        return (Cortar(Nome(host), 60), campanha);
    }

    public static (string? Dispositivo, string? Sistema, string? Navegador) DoUserAgent(string? ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return (null, null, null);
        var sistema = ua switch
        {
            _ when ua.Contains("iPhone") => "iPhone",
            _ when ua.Contains("iPad") => "iPad",
            _ when ua.Contains("Android") => "Android",
            _ when ua.Contains("Windows") => "Windows",
            _ when ua.Contains("Mac OS X") => "Mac",
            _ when ua.Contains("Linux") => "Linux",
            _ => "outro",
        };
        var dispositivo = sistema is "iPad" || (ua.Contains("Android") && !ua.Contains("Mobile")) ? "tablet"
            : sistema is "iPhone" || ua.Contains("Mobile") ? "telemóvel" : "computador";
        var navegador = ua switch
        {
            _ when ua.Contains("FBAN") || ua.Contains("FBAV") => "Facebook (app)",
            _ when ua.Contains("Instagram") => "Instagram (app)",
            _ when ua.Contains("Edg/") => "Edge",
            _ when ua.Contains("SamsungBrowser") => "Samsung",
            _ when ua.Contains("OPR/") => "Opera",
            _ when ua.Contains("Firefox/") => "Firefox",
            _ when ua.Contains("Chrome/") || ua.Contains("CriOS") => "Chrome",
            _ when ua.Contains("Safari/") => "Safari",
            _ => "outro",
        };
        return (dispositivo, sistema, navegador);
    }

    public static bool EBot(string? ua) => string.IsNullOrWhiteSpace(ua) || BotRx().IsMatch(ua);

    [GeneratedRegex(@"bot|crawl|spider|slurp|headless|lighthouse|pagespeed|preview|facebookexternalhit|python|curl|wget|httpclient|playwright|puppeteer", RegexOptions.IgnoreCase)]
    private static partial Regex BotRx();

    private static string? Id(string? s)
        => s is { Length: >= 8 and <= 40 } && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? s : null;

    private static string? Cortar(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }

    private static (string? Caminho, string? Query) Partir(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, null);
        var i = url.IndexOf('?');
        return i < 0 ? (url, null) : (url[..i], url[(i + 1)..]);
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query)) return d;
        foreach (var par in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = par.Split('=', 2);
            try { d.TryAdd(Uri.UnescapeDataString(kv[0]), kv.Length > 1 ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : ""); }
            catch (UriFormatException) { }
        }
        return d;
    }

    private static string? Host(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u.Host.ToLowerInvariant() : null;

    /// <summary>Só o domínio + caminho do referrer (sem query — pode trazer dados pessoais).</summary>
    private static string? SoOrigem(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? $"{u.Host}{u.AbsolutePath}" : null;

    private static (DateTime, DateTime) Intervalo(DateOnly de, DateOnly ate)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber > 400) de = ate.AddDays(-400);
        var deUtc = TimeZoneInfo.ConvertTimeToUtc(de.ToDateTime(TimeOnly.MinValue), Lisboa);
        var ateUtc = TimeZoneInfo.ConvertTimeToUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue), Lisboa);
        return (deUtc, ateUtc);
    }
}
