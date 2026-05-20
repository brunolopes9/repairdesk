using System.Globalization;
using System.Text.RegularExpressions;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 124: heurísticas para extrair campos estruturados de texto de encomendas de
/// fornecedor. Tenta primeiro parsers específicos (Tudo4Mobile, Molano), cai para genérico.
///
/// Confiança baixa por definição — Bruno deve confirmar manualmente todos os campos
/// sugeridos. O objectivo é reduzir digitação, não automatizar 100%.
/// </summary>
public static class SupplierPdfParser
{
    public static SupplierPdfParseResult Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SupplierPdfParseResult(null, null, null, null, ParseConfidence.None, Array.Empty<SupplierPdfItem>());

        // Detecta fornecedor por substring no texto bruto.
        var supplier = DetectSupplier(text);

        return supplier switch
        {
            "Tudo4Mobile" => ParseTudo4Mobile(text),
            _ => ParseGeneric(text, supplier),
        };
    }

    private static string? DetectSupplier(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("tudo4mobile")) return "Tudo4Mobile";
        if (lower.Contains("molano")) return "Molano";
        if (lower.Contains("lcphones") || lower.Contains("lc phones")) return "LCPhones";
        if (lower.Contains("utopya")) return "Utopya";
        return null;
    }

    /// <summary>
    /// Parser específico para emails da Tudo4Mobile. Formato típico:
    /// <code>
    /// Order ID: 161144
    /// Date Added: 19/05/2026
    /// Total: 47.20€
    /// Product Model Quantity Price Total
    /// Touch+Display+Frame Samsung Galaxy A15 ... 148118 1 33.90€ 33.90€
    /// </code>
    /// </summary>
    private static SupplierPdfParseResult ParseTudo4Mobile(string text)
    {
        var orderId = Match(text, @"Order\s*ID:?\s*(\d+)");
        var dateText = Match(text, @"Date Added:?\s*(\d{1,2}/\d{1,2}/\d{4})");
        var total = ParseEurAmount(Match(text, @"Total(?:\s*to pay)?:?\s*([\d.,]+)\s*€"));
        DateTime? date = null;
        if (DateTime.TryParseExact(dateText, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            date = parsed;

        // Items: linhas com `Descricao Model Qty Preco Total`. Cada coluna pode estar em linha
        // separada quando o PDF é exportado do Gmail. Heurística: capturar linhas com EUR.
        var items = ExtractItemsByEur(text);

        return new SupplierPdfParseResult(
            SupplierName: "Tudo4Mobile",
            OrderId: orderId,
            TotalCents: total,
            DateAdded: date,
            Confidence: ParseConfidence.High,
            Items: items);
    }

    private static SupplierPdfParseResult ParseGeneric(string text, string? supplier)
    {
        // Procura qualquer linha com "Total: 47,20€" ou similar.
        var total = ParseEurAmount(Match(text, @"(?:Total|TOTAL)[\s:]*([\d.,]+)\s*€"))
            ?? ParseEurAmount(Match(text, @"([\d.,]+)\s*€\s*(?:total|TOTAL)"));
        return new SupplierPdfParseResult(
            SupplierName: supplier,
            OrderId: null,
            TotalCents: total,
            DateAdded: null,
            Confidence: total is not null ? ParseConfidence.Low : ParseConfidence.None,
            Items: ExtractItemsByEur(text));
    }

    private static IReadOnlyList<SupplierPdfItem> ExtractItemsByEur(string text)
    {
        // Cada linha com ≥1 amount em EUR mas não "Total:" / "VAT" / "Shipping" → candidato a item.
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var items = new List<SupplierPdfItem>();
        var eurRegex = new Regex(@"([\d.,]+)\s*€", RegexOptions.Compiled);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var lower = line.ToLowerInvariant();
            if (lower.StartsWith("total") || lower.StartsWith("sub-total")
                || lower.StartsWith("vat") || lower.StartsWith("iva")
                || lower.Contains("shipping") || lower.Contains("transporte"))
                continue;

            var matches = eurRegex.Matches(line);
            if (matches.Count == 0) continue;

            // O último amount na linha é tipicamente o "Total da linha".
            var lineTotal = ParseEurAmount(matches[^1].Groups[1].Value);
            if (lineTotal is null || lineTotal == 0) continue;

            // Descrição = texto antes do primeiro número.
            var firstNumIdx = line.IndexOf(matches[0].Value, StringComparison.Ordinal);
            var description = firstNumIdx > 0 ? line[..firstNumIdx].Trim() : line.Trim();
            if (description.Length == 0 || description.Length > 300) continue;

            items.Add(new SupplierPdfItem(description, 1, lineTotal.Value));
            if (items.Count >= 20) break; // hard cap defensivo
        }
        return items;
    }

    private static string? Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static int? ParseEurAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // PT format: 47,20 OR EN format: 47.20. Aceita ambos.
        var normalised = raw.Replace(" ", "");
        if (normalised.Contains(',') && normalised.Contains('.'))
        {
            // 1.234,56 → 1234,56 → 1234.56
            normalised = normalised.Replace(".", "").Replace(",", ".");
        }
        else
        {
            normalised = normalised.Replace(",", ".");
        }
        if (decimal.TryParse(normalised, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d * 100);
        return null;
    }
}

public sealed record SupplierPdfParseResult(
    string? SupplierName,
    string? OrderId,
    int? TotalCents,
    DateTime? DateAdded,
    ParseConfidence Confidence,
    IReadOnlyList<SupplierPdfItem> Items);

public sealed record SupplierPdfItem(string Description, int Quantity, int LineTotalCents);

public enum ParseConfidence
{
    None = 0,
    Low = 1,
    /// <summary>Parser específico identificou o fornecedor + total + orderId.</summary>
    High = 2,
}
