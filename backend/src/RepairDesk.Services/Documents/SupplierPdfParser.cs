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
    /// Parser específico para emails da Tudo4Mobile. Formato típico (PDF Gmail quebra em 2 linhas):
    /// <code>
    /// Order ID: 161144
    /// Date Added: 19/05/2026
    /// Touch+Display+Frame Samsung Galaxy A15 4G/A155/A15 5G/A156 Service Pack
    /// Black 148118 1 33.90€ 33.90€
    /// Sub-Total (Without Tax): 33.90€
    /// Express Shipping: 5.50€
    /// VAT (23%): 7.80€
    /// Total: 47.20€
    /// </code>
    /// Custo per-unit = total (com portes + IVA) distribuído proporcionalmente pelo line subtotal.
    /// </summary>
    private static SupplierPdfParseResult ParseTudo4Mobile(string text)
    {
        var orderId = Match(text, @"Order\s*ID:?\s*(\d+)");
        var dateText = Match(text, @"Date Added:?\s*(\d{1,2}/\d{1,2}/\d{4})");
        var orderTotal = ParseEurAmount(Match(text, @"Total(?:\s*to pay)?:?\s*([\d.,]+)\s*€"));
        var orderSubtotal = ParseEurAmount(Match(text, @"Sub-?Total\s*\(Without Tax\)\s*:?\s*([\d.,]+)\s*€"))
            ?? ParseEurAmount(Match(text, @"Sub-?Total\s*:?\s*([\d.,]+)\s*€"));

        DateTime? date = null;
        if (DateTime.TryParseExact(dateText, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            date = parsed;

        // Sprint 134: items extraídos com junção de linha-anterior quando descrição quebra,
        // depois enriquecidos com brand/model/supplierSku/quantity, e custo unitário ajustado
        // pelo total (proporcional ao subtotal).
        var rawItems = ExtractItemsByEur(text);
        var items = AdjustItemsForOrderTotal(rawItems, orderSubtotal, orderTotal);

        return new SupplierPdfParseResult(
            SupplierName: "Tudo4Mobile",
            OrderId: orderId,
            TotalCents: orderTotal,
            DateAdded: date,
            Confidence: ParseConfidence.High,
            Items: items);
    }

    private static SupplierPdfParseResult ParseGeneric(string text, string? supplier)
    {
        // Procura qualquer linha com "Total: 47,20€" ou similar.
        var total = ParseEurAmount(Match(text, @"(?:Total|TOTAL)[\s:]*([\d.,]+)\s*€"))
            ?? ParseEurAmount(Match(text, @"([\d.,]+)\s*€\s*(?:total|TOTAL)"));
        var subtotal = ParseEurAmount(Match(text, @"Sub-?Total\s*\(Without Tax\)\s*:?\s*([\d.,]+)\s*€"));
        var items = AdjustItemsForOrderTotal(ExtractItemsByEur(text), subtotal, total);
        return new SupplierPdfParseResult(
            SupplierName: supplier,
            OrderId: null,
            TotalCents: total,
            DateAdded: null,
            Confidence: total is not null ? ParseConfidence.Low : ParseConfidence.None,
            Items: items);
    }

    /// <summary>
    /// Sprint 134: distribui o total da encomenda (com portes + IVA) pelos items proporcionalmente
    /// ao subtotal de cada linha. Garante que o custo unitário reflecte o que Bruno paga ao
    /// fornecedor por unidade — não o preço lista sem IVA.
    /// Fallback: se não consegue calcular, mantém o LineTotalCents original.
    /// </summary>
    private static IReadOnlyList<SupplierPdfItem> AdjustItemsForOrderTotal(
        IReadOnlyList<SupplierPdfItem> raw, int? orderSubtotal, int? orderTotal)
    {
        if (raw.Count == 0) return raw;
        if (orderSubtotal is null || orderTotal is null || orderSubtotal.Value <= 0)
            return raw;

        // Soma o subtotal real dos items extraídos para checkar consistência. Se diverge muito
        // do orderSubtotal anunciado no PDF, é prudente NÃO ajustar (parser pode ter apanhado
        // linhas duplicadas ou itens fantasma).
        var extractedSubtotal = raw.Sum(i => i.LineTotalCents);
        var divergencia = Math.Abs(extractedSubtotal - orderSubtotal.Value);
        if (divergencia > orderSubtotal.Value / 10) return raw; // >10% diff → desconfio

        var adjusted = new List<SupplierPdfItem>(raw.Count);
        foreach (var item in raw)
        {
            // unit total com portes+IVA = (line subtotal / order subtotal) × order total
            var weighted = (long)item.LineTotalCents * orderTotal.Value / orderSubtotal.Value;
            adjusted.Add(item with { LineTotalCents = (int)weighted });
        }
        return adjusted;
    }

    /// <summary>
    /// Extrai items das linhas que contêm "X €". Quando a linha com € tem pouco texto antes do
    /// primeiro número (caso comum em PDF de email onde a descrição quebra em 2 linhas), junta a
    /// linha imediatamente anterior à descrição. Resultado: descrição completa "Touch+Display+Frame
    /// Samsung Galaxy A15 ... Service Pack Black".
    /// Sprint 134: também extrai brand/model/supplierSku/quantity para auto-fill do form.
    /// </summary>
    private static IReadOnlyList<SupplierPdfItem> ExtractItemsByEur(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var items = new List<SupplierPdfItem>();
        var eurRegex = new Regex(@"([\d.,]+)\s*€", RegexOptions.Compiled);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            var lower = line.ToLowerInvariant();
            // Filtra metalinhas (totals, taxes, shipping, header da tabela "Product Model ...").
            if (lower.StartsWith("total") || lower.StartsWith("sub-total")
                || lower.StartsWith("vat") || lower.StartsWith("iva")
                || lower.Contains("shipping") || lower.Contains("transporte")
                || lower.StartsWith("product model") || lower.StartsWith("product\t"))
                continue;

            var matches = eurRegex.Matches(line);
            if (matches.Count == 0) continue;

            var lineTotal = ParseEurAmount(matches[^1].Groups[1].Value);
            if (lineTotal is null || lineTotal == 0) continue;

            // Texto antes do primeiro número-com-€ (ex: "Black 148118 1 ").
            var firstEurIdx = line.IndexOf(matches[0].Value, StringComparison.Ordinal);
            var beforeFirstEur = firstEurIdx > 0 ? line[..firstEurIdx].Trim() : "";

            // Sprint 134: se beforeFirstEur tem pouco texto descritivo (≤3 palavras "reais",
            // tipicamente "Black 148118 1"), junta a linha anterior — provavelmente é continuação.
            var descricao = beforeFirstEur;
            var palavrasReais = beforeFirstEur.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Count(w => w.Any(char.IsLetter) && w.Length >= 3);
            if (palavrasReais <= 3 && i > 0)
            {
                var prev = lines[i - 1].Trim();
                var prevLower = prev.ToLowerInvariant();
                // Só usa a linha anterior se NÃO for metalinha — se for header/total skipped seria erro.
                if (prev.Length > 0
                    && !prevLower.StartsWith("total") && !prevLower.StartsWith("sub-total")
                    && !prevLower.StartsWith("vat") && !prevLower.StartsWith("iva")
                    && !prevLower.Contains("shipping") && !prevLower.Contains("transporte")
                    && !prevLower.StartsWith("product model")
                    && !eurRegex.IsMatch(prev))
                {
                    descricao = (prev + " " + beforeFirstEur).Trim();
                }
            }

            if (descricao.Length == 0 || descricao.Length > 400) continue;

            // Quantidade: último número antes do primeiro € na descrição combinada.
            // Padrão Tudo4Mobile: "... {supplierSku} {qty} {price}€ {total}€"
            var qtyMatch = Regex.Match(descricao, @"\s(\d{1,3})\s*$");
            var quantity = qtyMatch.Success && int.TryParse(qtyMatch.Groups[1].Value, out var q) && q > 0 && q < 1000
                ? q : 1;

            // Limpa: remove o supplier SKU (6+ dígitos isolados) e a qty final, fica só descrição.
            var cleaned = Regex.Replace(descricao, @"\s+\d{5,}\s+\d{1,3}\s*$", "").Trim();
            if (cleaned.Length == 0) cleaned = descricao;

            var (brand, model) = ExtractBrandModel(cleaned);

            items.Add(new SupplierPdfItem(
                Description: cleaned,
                Quantity: quantity,
                LineTotalCents: lineTotal.Value,
                Brand: brand,
                Model: model));

            if (items.Count >= 20) break;
        }
        return items;
    }

    /// <summary>
    /// Sprint 134: heurística para extrair marca + modelo da descrição. Procura primeira marca
    /// conhecida; modelo = palavras seguintes até stop-word (cor, "Service Pack", etc).
    /// </summary>
    private static (string? Brand, string? Model) ExtractBrandModel(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return (null, null);

        // Marcas comuns no mercado de reparações PT. Ordem importa só para evitar matches parciais
        // (ex: "OnePlus" antes de "One"). Aqui não há conflitos.
        var brands = new[] {
            "Apple", "Samsung", "Xiaomi", "Huawei", "Honor", "Realme", "OnePlus", "Google",
            "Sony", "Nokia", "ZTE", "Oppo", "Vivo", "Motorola", "LG", "Asus", "Lenovo",
            "Acer", "HP", "Dell", "Microsoft", "Surface", "MacBook", "iPad", "iPhone",
        };
        var foundBrand = brands.FirstOrDefault(b => Regex.IsMatch(description, $@"\b{Regex.Escape(b)}\b", RegexOptions.IgnoreCase));
        if (foundBrand is null) return (null, null);

        // Apple guideline: "iPhone X" e "MacBook Pro" são tratados como modelo, brand="Apple".
        var brand = foundBrand switch
        {
            "iPhone" or "iPad" or "MacBook" => "Apple",
            _ => foundBrand,
        };

        // Modelo = texto a seguir à marca. Para iPhone/iPad/MacBook, inclui a palavra "iPhone X".
        var brandPattern = foundBrand is "iPhone" or "iPad" or "MacBook"
            ? $@"\b({Regex.Escape(foundBrand)}\b[^,\n]*?)\b(?:Black|White|Blue|Red|Green|Gold|Silver|Pink|Purple|Grey|Gray|Yellow|Orange|Service\s*Pack|OEM|[A-Z]?\d{{5,}})\b"
            : $@"\b{Regex.Escape(foundBrand)}\b\s+([^,\n]*?)\b(?:Black|White|Blue|Red|Green|Gold|Silver|Pink|Purple|Grey|Gray|Yellow|Orange|Service\s*Pack|OEM|[A-Z]?\d{{5,}})\b";

        var modelMatch = Regex.Match(description, brandPattern, RegexOptions.IgnoreCase);
        if (modelMatch.Success)
        {
            var model = modelMatch.Groups[1].Value.Trim();
            // Limpa whitespace múltiplo.
            model = Regex.Replace(model, @"\s+", " ");
            if (model.Length > 0 && model.Length <= 100) return (brand, model);
        }

        // Fallback: tudo entre a marca e o fim da descrição (truncado a 60 chars).
        var idx = description.IndexOf(foundBrand, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var afterBrand = foundBrand is "iPhone" or "iPad" or "MacBook"
                ? description[idx..]
                : description[(idx + foundBrand.Length)..];
            afterBrand = Regex.Replace(afterBrand.Trim(), @"\s+", " ");
            if (afterBrand.Length > 60) afterBrand = afterBrand[..60].TrimEnd();
            return (brand, afterBrand.Length > 0 ? afterBrand : null);
        }
        return (brand, null);
    }

    private static string? Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static int? ParseEurAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // PT format: 47,20 OR EN format: 47.20. PT milhares: 1.234,56. EN milhares: 1,234.56.
        // Regra: o separador que aparece **último** é o decimal.
        var normalised = raw.Replace(" ", "");
        var lastComma = normalised.LastIndexOf(',');
        var lastDot = normalised.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
                normalised = normalised.Replace(".", "").Replace(",", ".");  // PT: 1.234,56 → 1234.56
            else
                normalised = normalised.Replace(",", "");                    // EN: 1,234.56 → 1234.56
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

public sealed record SupplierPdfItem(
    string Description,
    int Quantity,
    /// <summary>Sprint 134: total da linha em cêntimos. Após AdjustItemsForOrderTotal, inclui IVA e portes proporcionais.</summary>
    int LineTotalCents,
    /// <summary>Sprint 134: marca extraída da descrição (Samsung, Apple, etc). Null se não reconhecida.</summary>
    string? Brand = null,
    /// <summary>Sprint 134: modelo extraído (Galaxy A15, iPhone 12, etc). Null se não reconhecido.</summary>
    string? Model = null);

public enum ParseConfidence
{
    None = 0,
    Low = 1,
    /// <summary>Parser específico identificou o fornecedor + total + orderId.</summary>
    High = 2,
}
