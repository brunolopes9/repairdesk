using System.Globalization;
using System.Text;

namespace RepairDesk.DAL.Persistence;

/// <summary>
/// Sprint 562: pesquisa por palavras. "xiaomi 13t pro" encontra "Xiaomi 13T / 13T Pro": cada palavra tem de
/// aparecer (em qualquer ordem e campo), em vez de a frase inteira seguida. Fonte única para as listas.
/// Sprint 591: sem olhar a acentos nem maiúsculas — "pelicula" encontra "Película", "ECRA" encontra "Ecrã".
/// </summary>
public static class PesquisaTexto
{
    /// <summary>Collation do SQL Server que ignora maiúsculas (CI) e acentos (AI).</summary>
    public const string Collation = "Latin1_General_100_CI_AI";

    private static readonly char[] Separadores = [' ', '\t', '/', ',', ';', '+', '(', ')', '|'];

    public static IReadOnlyList<string> Termos(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? []
            : query.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(Padrao)
                .ToList();

    /// <summary>"%termo%" para LIKE, com os caracteres especiais (%, _, [) tratados como texto.</summary>
    public static string Padrao(string termo)
        => "%" + termo.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

    /// <summary>
    /// <paramref name="texto"/> contém o termo de <paramref name="padrao"/> (feito por <see cref="Padrao"/>), sem olhar
    /// a acentos nem maiúsculas. Nas queries traduz-se para <c>texto COLLATE Latin1_General_100_CI_AI LIKE padrao</c>
    /// (ver <see cref="AppDbContext"/>); fora do SQL Server (testes em memória) corre este código.
    /// </summary>
    public static bool Contem(string? texto, string padrao)
    {
        if (texto is null) return false;
        var termo = padrao[1..^1].Replace("[[]", "[").Replace("[%]", "%").Replace("[_]", "_");
        return SemAcentos(texto).Contains(SemAcentos(termo), StringComparison.OrdinalIgnoreCase);
    }

    public static string SemAcentos(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
