using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Artigos;

namespace RepairDesk.Services.Loja;

/// <summary>O que o «Pôr na loja» automático conseguiu fazer — o ecrã diz ao dono o que falta.</summary>
public sealed record ProdutoAutomaticoResultado(ProdutoLojaDto Produto, int FotosImportadas, bool TextoGerado, string? Aviso);

public interface IProdutoAutomatico
{
    /// <summary>Cria o produto da loja a partir de um artigo do stock: título, marca, preço, fotos do fornecedor e texto da IA.
    /// Fica em rascunho — o dono revê e publica.</summary>
    Task<ProdutoAutomaticoResultado> CriarDoArtigoAsync(Guid artigoId, CancellationToken ct = default);

    /// <summary>Vai buscar as fotos oficiais do fornecedor para um produto que ainda não as tem.</summary>
    Task<ProdutoAutomaticoResultado> FotosDoFornecedorAsync(Guid produtoId, CancellationToken ct = default);
}

/// <summary>
/// 23/11: «como é que eu mando os produtos para a loja?» — até aqui eram 4 passos à mão (título, IA, fotos, publicar).
/// Agora um clique no artigo faz tudo o que é automático; o dono só revê o preço e carrega em Publicar.
/// Fotos: as oficiais do fornecedor (4Phones, pelo SKU da fatura), descarregadas e guardadas no nosso armazenamento —
/// o site nunca depende do CDN do fornecedor. Cada passo pode falhar sozinho sem estragar os outros.
/// </summary>
public sealed partial class ProdutoAutomatico : IProdutoAutomatico
{
    private const int MaxFotos = 6;
    private const long MaxBytes = 15 * 1024 * 1024;

    private readonly ILojaService _loja;
    private readonly IArtigoRepository _artigos;
    private readonly IFichaFornecedor _ficha;
    private readonly ITextoProdutoIa _ia;
    private readonly HttpClient _http;
    private readonly ILogger<ProdutoAutomatico> _logger;

    public ProdutoAutomatico(ILojaService loja, IArtigoRepository artigos, IFichaFornecedor ficha, ITextoProdutoIa ia,
        HttpClient http, ILogger<ProdutoAutomatico> logger)
    {
        _loja = loja;
        _artigos = artigos;
        _ficha = ficha;
        _ia = ia;
        _http = http;
        _logger = logger;
    }

    public async Task<ProdutoAutomaticoResultado> CriarDoArtigoAsync(Guid artigoId, CancellationToken ct = default)
    {
        var artigo = await _artigos.FindByIdAsync(artigoId, ct) ?? throw new NotFoundException("Artigo", artigoId);
        // Artigo já na loja ou sem preço: o LojaService recusa com a mensagem certa (fonte única das regras).
        var ficha = await _ficha.ProcurarProdutoAsync(Codigos(artigo), ct, Nomes(artigo));
        var titulo = Titulo(artigo);
        var req = new GuardarProdutoLojaRequest(
            Titulo: titulo,
            Marca: Marca(ficha?.Marca, titulo),
            Modelo: null,
            Categoria: Categoria(artigo.Categoria, titulo),
            Estado: LojaEstado.Novo,
            Grau: null,
            GarantiaMeses: 36,
            RegimeIva: LojaRegimeIva.Normal,
            Origem: LojaOrigem.Proprio,
            FornecedorId: null,
            Disponibilidade: null,
            Preco: (artigo.PrecoVendaCents ?? 0) / 100m,
            Custo: null,
            Cor: null, RamGb: null, ArmazenamentoGb: null,
            Descricao: null, Specs: null, Slug: null, SeoTitulo: null, SeoDescricao: null,
            ArtigoId: artigo.Id);
        var produto = await _loja.CreateAsync(req, ct);

        var avisos = new List<string>();
        var texto = await TentarTexto(produto.Id, req, avisos, ct);
        var fotos = ficha is null ? 0 : await ImportarFotos(produto.Id, ficha, titulo, avisos, ct);
        if (ficha is null) avisos.Add("Não encontrei este artigo no site do fornecedor — põe as fotos à mão.");
        return new ProdutoAutomaticoResultado(await _loja.GetAsync(produto.Id, ct), fotos, texto, Juntar(avisos));
    }

    public async Task<ProdutoAutomaticoResultado> FotosDoFornecedorAsync(Guid produtoId, CancellationToken ct = default)
    {
        var produto = await _loja.GetAsync(produtoId, ct);
        var codigos = new List<string>();
        var nomes = new List<string>();
        if (produto.ArtigoId is { } aid && await _artigos.FindByIdAsync(aid, ct) is { } artigo)
        {
            codigos.AddRange(Codigos(artigo));
            nomes.AddRange(Nomes(artigo));
        }
        if (!string.IsNullOrWhiteSpace(produto.Modelo)) codigos.Add(produto.Modelo);
        var ficha = codigos.Count + nomes.Count == 0 ? null : await _ficha.ProcurarProdutoAsync(codigos, ct, nomes);
        if (ficha is null || ficha.Imagens.Count == 0)
            throw new ValidationException("sem_fotos_fornecedor",
                produto.ArtigoId is null
                    ? "Liga o produto a um artigo do stock (com o código do fornecedor) para eu encontrar as fotos."
                    : "Não encontrei fotos deste artigo no site do fornecedor.");
        var avisos = new List<string>();
        var n = await ImportarFotos(produtoId, ficha, produto.Titulo, avisos, ct, jaTem: produto.Fotos.Count);
        return new ProdutoAutomaticoResultado(await _loja.GetAsync(produtoId, ct), n, false, Juntar(avisos));
    }

    private async Task<bool> TentarTexto(Guid id, GuardarProdutoLojaRequest req, List<string> avisos, CancellationToken ct)
    {
        try
        {
            var t = await _ia.GerarAsync(req, ct);
            await _loja.UpdateAsync(id, req with
            {
                Descricao = t.Descricao,
                Specs = t.Specs,
                SeoTitulo = Cortar(t.SeoTitulo, 70),
                SeoDescricao = Cortar(t.SeoDescricao, 170),
            }, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Texto automático do produto {Id} falhou.", id);
            avisos.Add("A IA não conseguiu escrever a descrição agora — carrega em «Gerar com IA» no produto.");
            return false;
        }
    }

    private async Task<int> ImportarFotos(Guid id, FichaProduto ficha, string titulo, List<string> avisos, CancellationToken ct, int jaTem = 0)
    {
        var importadas = 0;
        foreach (var url in ficha.Imagens.Take(Math.Max(0, MaxFotos - jaTem)))
        {
            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;
                var tipo = resp.Content.Headers.ContentType?.MediaType ?? "";
                if (tipo is not ("image/jpeg" or "image/png" or "image/webp")) continue;
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length is 0 or > (int)MaxBytes) continue;

                var nome = Path.GetFileName(new Uri(url).AbsolutePath);
                var ticket = await _loja.PedirUploadFotoAsync(id, new PedirUploadFotoRequest(nome, tipo, bytes.Length, titulo), ct);
                using var put = new HttpRequestMessage(HttpMethod.Put, ticket.UploadUrl) { Content = new ByteArrayContent(bytes) };
                put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipo);
                using var r = await _http.SendAsync(put, ct);
                if (!r.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Upload da foto {Url} falhou: {Status}", url, r.StatusCode);
                    continue;
                }
                await _loja.ConfirmarFotoAsync(id, ticket.FotoId, ct);
                importadas++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
            {
                _logger.LogInformation(ex, "Foto do fornecedor {Url} indisponível.", url);
            }
        }
        if (ficha.Imagens.Count > 0 && importadas == 0) avisos.Add("O fornecedor tem fotos mas não as consegui descarregar — tenta outra vez.");
        return importadas;
    }

    // ---- Regras puras (testadas) ----------------------------------------------------------------

    public static IReadOnlyList<string> Codigos(Artigo a)
    {
        var c = new List<string>();
        c.AddRange(a.Lotes.Select(l => l.SkuFornecedor).Where(s => !string.IsNullOrWhiteSpace(s))!);
        if (!string.IsNullOrWhiteSpace(a.CodigoFabricante)) c.Add(a.CodigoFabricante);
        c.AddRange(a.Codigos.Select(x => x.Codigo));
        return c.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Nomes originais das linhas da fatura (como o fornecedor os escreve no site) — para procurar pelo nome.</summary>
    public static IReadOnlyList<string> Nomes(Artigo a)
        => a.Lotes.OrderByDescending(l => l.CreatedAt).Select(l => l.Descricao)
            .Append(a.NomeFatura ?? "").Append(a.Nome).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!;

    /// <summary>Nome da fatura ou do artigo, legível (sem maiúsculas a gritar) e sem «All colours». A qualidade
    /// (Service Pack, Original Remaded…) fica — a IA usa-a para explicar a peça.</summary>
    public static string Titulo(Artigo a)
    {
        var nome = ArtigoService.NomeLegivel(string.IsNullOrWhiteSpace(a.NomeFatura) ? a.Nome : a.NomeFatura!);
        nome = Ruido().Replace(nome, " ");
        nome = Regex.Replace(nome, @"\(\s*\)", " ");
        nome = Regex.Replace(nome, @"\s{2,}", " ").Trim(' ', '-', '–', ',');
        return nome.Length > 150 ? nome[..150].TrimEnd() : nome;
    }

    private static readonly (Regex Re, string Marca)[] Marcas =
    [
        (new(@"\b(iphone|ipad|airpods|apple|macbook|imac)\b", RegexOptions.IgnoreCase), "Apple"),
        (new(@"\b(samsung|galaxy)\b", RegexOptions.IgnoreCase), "Samsung"),
        (new(@"\b(xiaomi|redmi|poco)\b", RegexOptions.IgnoreCase), "Xiaomi"),
        (new(@"\bhuawei\b", RegexOptions.IgnoreCase), "Huawei"),
        (new(@"\boppo\b", RegexOptions.IgnoreCase), "Oppo"),
        (new(@"\bhonor\b", RegexOptions.IgnoreCase), "Honor"),
        (new(@"\bmotorola|\bmoto\s", RegexOptions.IgnoreCase), "Motorola"),
        (new(@"\brixus\b", RegexOptions.IgnoreCase), "Rixus"),
        (new(@"\bhoco\b", RegexOptions.IgnoreCase), "Hoco"),
        (new(@"\b(playstation|ps5|ps4|dualsense)\b", RegexOptions.IgnoreCase), "Sony"),
    ];

    /// <summary>Marca do fornecedor (vendor) quando é uma marca a sério; senão, pelo título. Peças sem marca → «Genérico».</summary>
    public static string Marca(string? doFornecedor, string titulo)
    {
        if (!string.IsNullOrWhiteSpace(doFornecedor) && !Regex.IsMatch(doFornecedor, "4phones|tudo4mobile|mobilesentrix|oem|generic", RegexOptions.IgnoreCase))
            return Cortar(doFornecedor.Trim(), 60)!;
        foreach (var (re, m) in Marcas)
            if (re.IsMatch(titulo)) return m;
        return "Genérico";
    }

    public static LojaCategoria Categoria(string? categoriaArtigo, string titulo)
    {
        var c = $"{categoriaArtigo} {titulo}".ToLowerInvariant();
        if (Regex.IsMatch(c, @"pe[cç]a|bateria|ecr[aã]|display|lcd|oled|flex|conector|tampa|traseira|frame")) return LojaCategoria.Pecas;
        if (Regex.IsMatch(c, @"port[aá]til|computador|laptop|notebook")) return LojaCategoria.Computadores;
        if (Regex.IsMatch(c, @"\btablet|\bipad")) return LojaCategoria.Tablets;
        return LojaCategoria.Acessorios;
    }

    private static string? Cortar(string? s, int max) => s is null ? null : s.Length > max ? s[..max].TrimEnd() : s;
    private static string? Juntar(List<string> avisos) => avisos.Count == 0 ? null : string.Join(" ", avisos);

    [GeneratedRegex(@"\ball colou?rs?\b", RegexOptions.IgnoreCase)]
    private static partial Regex Ruido();
}
