using System.Text.RegularExpressions;

namespace RepairDesk.Services.Artigos;

/// <summary>
/// Sprint 582: o modelo do telemóvel a partir da descrição do lote (tal como veio na fatura do fornecedor, muitas vezes
/// em inglês). Vai para a fatura ao cliente, em português: "Película vidro curva – iPhone XR / 11". O texto original
/// do fornecedor fica só dentro do Mender.
/// </summary>
public static partial class ModeloDispositivo
{
    /// <summary>
    /// "Rixus Clear HD Full Curved Edge Tempered Glass For Apple iPhone XR, 11" → "iPhone XR / 11";
    /// "Samsung Galaxy A16 A165 - Full Arc Tempered Glass Black" → "Samsung Galaxy A16 A165";
    /// "Capa silicone Samsung A05s preta" → "Samsung A05s preta". Null quando não se reconhece marca nenhuma.
    /// </summary>
    public static string? Extrair(string? descricao, bool feminino = false)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return null;
        var m = MarcaRegex().Match(descricao);
        if (!m.Success) return null;
        var resto = descricao[m.Index..];
        // Corta o que vem depois do modelo: " - Tempered Glass", "(60Hz)", "With Fit Box"…
        resto = Cauda().Split(resto)[0];
        resto = PalavrasIngles().Replace(resto, " ");
        resto = AppleIphone().Replace(resto, "iPhone");
        resto = Iph().Replace(resto, "iPhone ");
        resto = CodigoInterno().Replace(resto, " ");
        resto = Virgulas().Replace(resto, " / ");
        resto = Barras().Replace(resto, " / ");
        resto = Espacos().Replace(resto, " ").Trim(' ', ',', '/', '-', '.');
        // Sprint 583: a cor concorda com o artigo — "Capa – iPhone 14 preta", "Cabo – … preto".
        foreach (var (en, masc, fem) in Cores) resto = Regex.Replace(resto, $@"\b{en}\b", feminino ? fem : masc, RegexOptions.IgnoreCase);
        return resto.Length is 0 or > 80 ? null : resto;
    }

    /// <summary>
    /// Texto da linha da fatura ao cliente para um lote de um artigo agrupado: "Película vidro curva – iPhone XR / 11".
    /// <paramref name="modeloManual"/> (escrito no balcão) ganha sempre. Sem modelo: a descrição do lote se começar pelo
    /// mesmo nome (ex.: "Cabo Hoco X88 Lightning 2M" num artigo "Cabo"), senão só o nome do artigo — nunca o texto
    /// do fornecedor noutra língua.
    /// </summary>
    public static string TextoFatura(string nomeArtigo, string descricaoLote, string? modeloManual = null)
    {
        if (!string.IsNullOrWhiteSpace(modeloManual)) return $"{nomeArtigo} – {modeloManual.Trim()}";
        if (Extrair(descricaoLote, Feminino(nomeArtigo)) is { } modelo) return $"{nomeArtigo} – {modelo}";
        return DescricaoServe(nomeArtigo, descricaoLote) ? descricaoLote.Trim() : nomeArtigo;
    }

    /// <summary>
    /// Sprint 583: a linha sairia só com o nome do artigo ("Película vidro") — o balcão pede o modelo à mão para a fatura
    /// (e a garantia) dizerem sempre para que telemóvel é.
    /// </summary>
    public static bool PrecisaModelo(string nomeArtigo, string descricaoLote)
        => Extrair(descricaoLote) is null && !DescricaoServe(nomeArtigo, descricaoLote);

    private static bool DescricaoServe(string nomeArtigo, string descricaoLote)
    {
        var primeira = PrimeiraPalavra(nomeArtigo);
        return descricaoLote.TrimStart().StartsWith(primeira, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string PrimeiraPalavra(string nome) => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    /// <summary>Género do artigo pela 1.ª palavra: "Capa", "Película" → feminino; "Cabo", "Carregador", "Cartão" → masculino.</summary>
    private static bool Feminino(string nomeArtigo)
    {
        var p = PrimeiraPalavra(nomeArtigo).ToLowerInvariant();
        return p.EndsWith('a') || p.EndsWith("ção") || p.EndsWith("gem") || p.EndsWith("ade");
    }

    private static readonly (string En, string Masc, string Fem)[] Cores =
    [
        ("Black", "preto", "preta"), ("White", "branco", "branca"), ("Blue", "azul", "azul"), ("Red", "vermelho", "vermelha"),
        ("Green", "verde", "verde"), ("Pink", "rosa", "rosa"), ("Clear", "transparente", "transparente"),
    ];

    [GeneratedRegex(@"\b(?:Apple\s+iPhone|iPhone|iph(?=\s)|Samsung|Galaxy|Xiaomi|Redmi|Poco|Oppo|Huawei|Honor|Realme|Motorola|Moto\s+[EG]\d|OnePlus|Google\s+Pixel|Pixel\s+\d|Nokia|Vivo|Microsoft\s+Surface|iPad)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MarcaRegex();

    [GeneratedRegex(@"\s+-\s+|\s*\(|\s+(?:With|Display|Battery|Full\s+Front|Tempered|Glass|Screen)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Cauda();

    [GeneratedRegex(@"\b(?:For|Black\s+OEM|OEM)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PalavrasIngles();

    [GeneratedRegex(@"Apple\s+iPhone", RegexOptions.IgnoreCase)]
    private static partial Regex AppleIphone();

    [GeneratedRegex(@"\biph\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Iph();

    // Códigos internos de fabricante (2411DRN47C, 23049PCD8G): 4 dígitos + 4 ou mais letras/dígitos.
    [GeneratedRegex(@"\b\d{4}[A-Z0-9]{4,}\b")]
    private static partial Regex CodigoInterno();

    [GeneratedRegex(@"(?:\s*/\s*)+")]
    private static partial Regex Barras();

    [GeneratedRegex(@"\s*,\s*")]
    private static partial Regex Virgulas();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Espacos();
}
