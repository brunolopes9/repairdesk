namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 171: validation rules pós-parse (regex parser, LLM, fingerprinting).
/// Detecta inconsistências comuns que indicam parsing mau:
/// - Soma dos lineTotalCents difere muito do totalCents (>5¢)
/// - DocumentDate no futuro (parsing leu mal a data)
/// - Quantity ou lineTotal negativos/zero (items inválidos)
/// - Items vazios mas totalCents > 0 (incoerente)
///
/// Quando há falhas, força Confidence=Low para Bruno saber que precisa de revisão extra.
/// Inspirado em prática Reddit (r/automation): "schema validation reduz erros mascarados".
/// </summary>
public static class ParseValidator
{
    public sealed record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> Warnings);

    public static ValidationResult Validate(SupplierPdfParseResult? parsed)
    {
        var warnings = new List<string>();
        if (parsed is null) return new(false, new[] { "Parsing falhou — sem dados." });

        // 1. Items presentes mas total zero (ou vice-versa).
        if (parsed.Items.Count > 0 && parsed.TotalCents is null)
            warnings.Add("Total da fatura não detectado.");
        if (parsed.Items.Count == 0 && (parsed.TotalCents ?? 0) > 0)
            warnings.Add($"Total {parsed.TotalCents / 100m:0.00}€ detectado mas sem items.");

        // 2. Soma items vs total — tolerância 5¢ para arredondamentos + portes.
        if (parsed.Items.Count > 0 && parsed.TotalCents is { } total && total > 0)
        {
            var sum = parsed.Items.Sum(i => i.LineTotalCents);
            var delta = Math.Abs(sum - total);
            if (delta > 5)
            {
                warnings.Add($"Soma items ({sum / 100m:0.00}€) != total fatura ({total / 100m:0.00}€) — diferença {delta / 100m:0.00}€.");
            }
        }

        // 3. Quantidade/preço inválidos.
        for (var i = 0; i < parsed.Items.Count; i++)
        {
            var item = parsed.Items[i];
            if (item.Quantity <= 0)
                warnings.Add($"Linha {i + 1} '{Truncate(item.Description, 40)}' tem quantidade {item.Quantity} (inválida).");
            if (item.LineTotalCents <= 0)
                warnings.Add($"Linha {i + 1} '{Truncate(item.Description, 40)}' tem valor {item.LineTotalCents / 100m:0.00}€ (inválido).");
        }

        // 4. Data no futuro (parsing confundiu dd/mm com mm/dd).
        if (parsed.DateAdded is { } date && date.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            warnings.Add($"Data da fatura {date:dd/MM/yyyy} está no futuro — provavelmente confusão dd/mm vs mm/dd.");
        }

        // 5. Data muito antiga (>3 anos) — provavelmente erro também.
        if (parsed.DateAdded is { } date2 && date2 < DateTime.UtcNow.AddYears(-3))
        {
            warnings.Add($"Data da fatura {date2:dd/MM/yyyy} muito antiga (>3 anos).");
        }

        return new(warnings.Count == 0, warnings);
    }

    /// <summary>Aplica validação ao result e rebaixa confidence se houver warnings.</summary>
    public static (SupplierPdfParseResult? Result, IReadOnlyList<string> Warnings) Apply(SupplierPdfParseResult? parsed)
    {
        var validation = Validate(parsed);
        if (validation.IsValid || parsed is null) return (parsed, validation.Warnings);

        // Rebaixar para Low quando há warnings, mesmo que parser inicial reportou High.
        var downgraded = parsed with { Confidence = ParseConfidence.Low };
        return (downgraded, validation.Warnings);
    }

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] + "…" : s;
}
