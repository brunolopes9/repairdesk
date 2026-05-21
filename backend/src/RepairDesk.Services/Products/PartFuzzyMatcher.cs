namespace RepairDesk.Services.Products;

/// <summary>
/// Sprint 157: fuzzy matcher para sugerir matches Part/Product internos quando o fornecedor
/// envia um SKU sem mapping ainda registado.
///
/// Estratégia:
/// - Tokenizar nome (split por espaços/-/_ + lowercase)
/// - Compute score híbrido:
///   * Token overlap (Jaccard similarity) — peso 60%
///   * Levenshtein no nome completo normalizado — peso 40%
/// - Devolve top N candidatos com score >= threshold (default 0.4)
///
/// Não é perfeito — Bruno revalida sempre na UI. Mas reduz drasticamente o trabalho
/// quando há 500+ Parts no inventário.
/// </summary>
public static class PartFuzzyMatcher
{
    public sealed record Candidate(Guid TargetId, string TargetName, double Score);

    /// <summary>
    /// Encontra top N candidatos para o nome do fornecedor.
    /// <paramref name="haystack"/>: lista de (id, nome) das Parts/Products candidatos.
    /// </summary>
    public static IReadOnlyList<Candidate> Find(
        string supplierName,
        IEnumerable<(Guid Id, string Name)> haystack,
        int topN = 3,
        double minScore = 0.4)
    {
        if (string.IsNullOrWhiteSpace(supplierName)) return Array.Empty<Candidate>();

        var supplierTokens = Tokenize(supplierName);
        var supplierNorm = Normalize(supplierName);

        return haystack
            .Select(h =>
            {
                var hayTokens = Tokenize(h.Name);
                var hayNorm = Normalize(h.Name);

                var jaccard = JaccardSimilarity(supplierTokens, hayTokens);
                var levRatio = LevenshteinRatio(supplierNorm, hayNorm);
                var score = jaccard * 0.6 + levRatio * 0.4;

                return new Candidate(h.Id, h.Name, score);
            })
            .Where(c => c.Score >= minScore)
            .OrderByDescending(c => c.Score)
            .Take(topN)
            .ToList();
    }

    /// <summary>Tokenize: lowercase + split por whitespace + remove stop words curtas.</summary>
    private static HashSet<string> Tokenize(string text)
    {
        var lower = Normalize(text);
        return lower.Split(new[] { ' ', '\t', '\n', '\r', '-', '_', '/', '+', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet();
    }

    /// <summary>Lower + remove acentos + colapsa espaços.</summary>
    private static string Normalize(string text)
    {
        var lower = text.ToLowerInvariant().Trim();
        var withoutDiacritics = new System.Text.StringBuilder();
        foreach (var c in lower.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                withoutDiacritics.Append(c);
        }
        return withoutDiacritics.ToString();
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double LevenshteinRatio(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        var maxLen = Math.Max(a.Length, b.Length);
        var distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var matrix = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        return matrix[a.Length, b.Length];
    }
}
