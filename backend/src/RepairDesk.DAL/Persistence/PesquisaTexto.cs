namespace RepairDesk.DAL.Persistence;

/// <summary>
/// Sprint 562: pesquisa por palavras. "xiaomi 13t pro" encontra "Xiaomi 13T / 13T Pro": cada palavra tem de
/// aparecer (em qualquer ordem e campo), em vez de a frase inteira seguida. Fonte única para as listas.
/// </summary>
internal static class PesquisaTexto
{
    private static readonly char[] Separadores = [' ', '\t', '/', ',', ';', '+', '(', ')', '|'];

    public static IReadOnlyList<string> Termos(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? []
            : query.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
}
