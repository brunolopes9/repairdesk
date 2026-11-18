namespace RepairDesk.DAL.Persistence;

/// <summary>
/// 18/11 (Bruno): «se pesquiso magnet devia aparecer magnético, gravity, ventilação…». As faturas vêm em inglês
/// (4Phones, LC Phones) e as pessoas escrevem em português — cada termo também procura os sinónimos do seu grupo.
/// A MESMA lista existe em frontend/src/lib/sinonimos.ts e no site (lopestech-site/js/loja.js) — manter iguais.
/// Um termo pertence a um grupo quando uma palavra do grupo começa por ele (≥ 3 letras) ou ele começa por uma
/// palavra do grupo (≥ 4 letras): «magnet» → «magnetico», «magnetic», «iman», «magsafe».
/// </summary>
public static class Sinonimos
{
    public static readonly string[][] Grupos =
    [
        ["magnetico", "magnetic", "magnet", "iman", "magsafe"],
        ["gravidade", "gravity"],
        ["ventilacao", "ventilador", "grelha", "vent", "air vent"],
        ["carregador", "charger", "adaptador", "adapter", "power adapter"],
        ["isqueiro", "car charger", "car adapter"],
        ["cabo", "cable"],
        ["capa", "case", "cover", "bookcase", "carteira", "bolsa"],
        ["pelicula", "vidro temperado", "tempered glass", "tempered", "screen protector", "protetor de ecra"],
        ["hidrogel", "hydrogel", "gel"],
        ["protetor de camara", "camera protector", "camera lens", "lente", "lens", "metal shield"],
        ["suporte", "holder", "mount"],
        ["retrovisor", "rearview", "espelho", "mirror"],
        ["auricular", "auriculares", "earphones", "headphones", "headset", "fones", "buds", "earbuds"],
        ["bateria", "battery"],
        ["ecra", "display", "lcd", "oled", "screen", "touch", "digitizer"],
        ["tampa", "back cover", "traseira", "back glass", "vidro traseiro", "housing"],
        ["powerbank", "power bank", "bateria externa"],
        ["conector de carga", "charging port", "charge port", "porta de carga", "dock"],
        ["camara", "camera"],
        ["altifalante", "speaker", "buzzer", "coluna"],
        ["transparente", "clear", "transparent"],
        ["preto", "preta", "black", "midnight"],
        ["branco", "branca", "white"],
        ["rosa", "pink"],
        ["azul", "blue"],
        ["dourado", "dourada", "gold"],
        ["verde", "green"],
        ["cinzento", "cinzenta", "gray", "grey"],
        ["titanio", "titanium"],
    ];

    /// <summary>O termo e os sinónimos (sem acentos, minúsculas). No máximo 8 variantes.</summary>
    public static IReadOnlyList<string> Variantes(string termo)
    {
        var t = PesquisaTexto.SemAcentos(termo).Trim().ToLowerInvariant();
        var lista = new List<string> { t };
        if (t.Length < 3) return lista;
        foreach (var g in Grupos)
            if (g.Any(w => w.StartsWith(t, StringComparison.Ordinal) || (w.Length >= 4 && t.StartsWith(w, StringComparison.Ordinal))))
                foreach (var w in g)
                    if (!lista.Contains(w)) lista.Add(w);
        return lista.Take(8).ToList();
    }
}
