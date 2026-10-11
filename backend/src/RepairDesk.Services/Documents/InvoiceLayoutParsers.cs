using System.Globalization;
using System.Text.RegularExpressions;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 560: leitores determinísticos para layouts de documento conhecidos — sem IA, sem custo e
/// sem "adivinhar". Cada leitor só devolve confiança <see cref="ParseConfidence.High"/> quando o
/// documento reconcilia (soma das linhas = total impresso); caso contrário fica Low e vai a revisão.
/// </summary>
public static class InvoiceLayoutParsers
{
    /// <summary>Tenta os layouts conhecidos; null = nenhum reconhecido (segue o parser genérico/IA).</summary>
    public static SupplierPdfParseResult? TryParse(string text)
    {
        if (CloudInvoice.Matches(text)) return CloudInvoice.Parse(text);
        if (MobileSentrix.Matches(text)) return MobileSentrix.Parse(text);
        return null;
    }

    /// <summary>
    /// Faturas emitidas pelo software certificado CloudInvoice (usado pela Tudo4Mobile e outros
    /// fornecedores PT). Linha de artigo tal como sai do PDF:
    /// <c>23% 19.56 €19.56 €3600198 UNI1.000Touch e lcd with frame sams galaxy</c>
    /// (taxa, total da linha, preço unitário, código, unidade+quantidade, descrição), podendo a
    /// descrição continuar nas linhas seguintes.
    /// </summary>
    public static class CloudInvoice
    {
        private static readonly Regex ItemLine = new(
            @"^(?<taxa>\d{1,2}(?:[.,]\d+)?)%\s+(?<total>[\d.,]+)\s*€\s*(?<unit>[\d.,]+)\s*€\s*(?<code>\S+)\s+(?<un>[A-Za-z]{1,5})(?<qty>\d+(?:[.,]\d+)?)(?<desc>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex DocHeader = new(
            @"^(?<tipo>Factura-Recibo|Fatura-Recibo|Factura Simplificada|Fatura Simplificada|Factura|Fatura|Nota de Cr[ée]dito)\s+(?<num>[A-Z]{1,5}\s+[A-Z0-9]+/\d+)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static bool Matches(string text)
            => text.Contains("CloudInvoice", StringComparison.OrdinalIgnoreCase)
               && text.Contains("TOTAL DOCUMENTO", StringComparison.OrdinalIgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var header = DocHeader.Match(text);
            var numero = header.Success ? Regex.Replace(header.Groups["num"].Value.Trim(), @"\s+", " ") : null;
            // Uma nota de crédito não é uma compra: o número é lido, mas fica para revisão humana.
            var notaCredito = header.Success && header.Groups["tipo"].Value.StartsWith("Nota", StringComparison.OrdinalIgnoreCase);

            var data = ParseIsoDate(Match(text, @"Data Emiss[ãa]o\s*\n\s*(\d{4}-\d{2}-\d{2})"));
            var total = ToCents(Match(text, @"TOTAL DOCUMENTO\s+([\d.,]+)\s*€"));
            // "Preço Unitário (c/IVA)": linhas já com IVA. Sem essa marca, os valores das linhas são s/IVA.
            var comIva = text.Contains("(c/IVA)", StringComparison.OrdinalIgnoreCase);

            var items = new List<(string Desc, decimal Qty, decimal Total, decimal Taxa)>();
            var lines = text.Replace("\r", string.Empty).Split('\n');
            var aberta = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                var m = ItemLine.Match(line);
                if (m.Success)
                {
                    items.Add((m.Groups["desc"].Value.Trim(), Dot(m.Groups["qty"].Value),
                        Dot(m.Groups["total"].Value), Dot(m.Groups["taxa"].Value) / 100m));
                    aberta = true;
                    continue;
                }
                if (!aberta) continue;
                if (line.Length == 0 || line.StartsWith("Os artigos", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Documento emitido", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Imposto", StringComparison.OrdinalIgnoreCase))
                {
                    aberta = false;
                    continue;
                }
                var last = items[^1];
                items[^1] = last with { Desc = $"{last.Desc} {line}".Trim() };
            }

            var parsedItems = items.Select(i => new SupplierPdfItem(
                Description: Regex.Replace(i.Desc, @"\s*\(\*\)\s*$", string.Empty).Trim(),
                Quantity: (int)Math.Round(i.Qty, MidpointRounding.AwayFromZero),
                LineTotalCents: (int)Math.Round((comIva ? i.Total : i.Total * (1 + i.Taxa)) * 100m, MidpointRounding.AwayFromZero)))
                .ToList();

            var fracionada = items.Any(i => i.Qty != Math.Round(i.Qty));
            var reconcilia = total is { } t && parsedItems.Count > 0 && Math.Abs(parsedItems.Sum(i => i.LineTotalCents) - t) <= 2;
            var confianca = numero is not null && data is not null && reconcilia && !notaCredito && !fracionada
                ? ParseConfidence.High
                : ParseConfidence.Low;

            return new SupplierPdfParseResult(SupplierName(text), numero, total, data, confianca, parsedItems);
        }

        private static string? SupplierName(string text)
        {
            if (text.Contains("tudo4mobile", StringComparison.OrdinalIgnoreCase)) return "Tudo4Mobile";
            // Rodapé CloudInvoice: "EMPRESA, LDA - Sede: Morada…"
            var sede = Match(text, @"^(.+?)\s+-\s+Sede:");
            return sede?.Trim();
        }
    }

    /// <summary>
    /// Documentos da MobileSentrix Europe (NL), preços sem IVA (entrega intracomunitária, art. 138.º):
    /// <list type="bullet">
    /// <item>Fatura (<c>Invoice Number INV-…</c> + <c>Número do pedido</c>) — documento fiscal.</item>
    /// <item>Resumo de encomenda (só <c>Order Number</c>) — o número lido é o da encomenda.</item>
    /// </list>
    /// Linha de artigo: descrição (1+ linhas) seguida de <c>SKU € preço qtd [qtd entregue] € subtotal</c>.
    /// O hífen deste PDF sai do extrator como um glifo privado (U+E088) ou desaparece; repõe-se "INV-".
    /// </summary>
    public static class MobileSentrix
    {
        private static readonly Regex ItemTail = new(
            @"^(?<desc>.*?)\s*(?<sku>\d{9,})\s+€\s*(?<unit>[\d.,]+)\s+(?<qty>\d+)(?:\s+\d+)?\s+€\s*(?<sub>[\d.,]+)\s*$",
            RegexOptions.Compiled);

        public static bool Matches(string text)
            => text.Contains("mobilesentrix", StringComparison.OrdinalIgnoreCase)
               && Regex.IsMatch(text, @"Order Number|Invoice Number", RegexOptions.IgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var fatura = Match(text, @"Invoice Number\s*\n?\s*(INV[^\d\s]?\d+)");
            if (fatura is not null) fatura = Regex.Replace(fatura, @"^INV\D?", "INV-", RegexOptions.IgnoreCase);
            var encomenda = Match(text, @"(?:N[úu]mero do pedido|Order Number|Order No\.?)\s*:?\s*\n?\s*(\d{6,})");
            var dataTxt = Match(text, @"(?:Invoice Date|Data do Pedido|Order Date)\s*\n\s*(\d{2}/\d{2}/\d{4})");
            DateTime? data = DateTime.TryParseExact(dataTxt, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
                : null;
            var total = ToCents(Match(text, @"(?:Total Geral|Grand Total):?\s*€\s*([\d.,]+)"));
            var envio = ToCents(Match(text, @"^(?:Envio|Shipping)\s*:?\s*€\s*([\d.,]+)"));

            var items = new List<SupplierPdfItem>();
            var lines = text.Replace("\r", string.Empty).Split('\n');
            var dentro = false;
            var buffer = new List<string>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!dentro)
                {
                    if (Regex.IsMatch(line, @"(?:Encomendado|Fulfilled|Qty|Ordered)\s+Subtotal", RegexOptions.IgnoreCase))
                        dentro = true;
                    continue;
                }
                if (Regex.IsMatch(line, @"^Total\s+\d+(?:\s+\d+)?$")) break;
                var m = ItemTail.Match(line);
                if (!m.Success)
                {
                    if (line.Length > 0) buffer.Add(line);
                    continue;
                }
                buffer.Add(m.Groups["desc"].Value);
                var desc = string.Join(' ', buffer.Where(b => b.Length > 0)).Trim();
                buffer.Clear();
                items.Add(new SupplierPdfItem(desc, int.Parse(m.Groups["qty"].Value, CultureInfo.InvariantCulture),
                    ToCents(m.Groups["sub"].Value) ?? 0));
            }
            if (envio is > 0) items.Add(new SupplierPdfItem("Envio (portes)", 1, envio.Value));

            var numero = fatura ?? encomenda;
            var reconcilia = total is { } t && items.Count > 0 && Math.Abs(items.Sum(i => i.LineTotalCents) - t) <= 2;
            var confianca = numero is not null && data is not null && reconcilia ? ParseConfidence.High : ParseConfidence.Low;
            return new SupplierPdfParseResult("MobileSentrix", numero, total, data, confianca, items,
                OrderReference: fatura is not null ? encomenda : null);
        }
    }

    // ---------- helpers ----------

    private static string? Match(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static DateTime? ParseIsoDate(string? s)
        => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;

    /// <summary>"1,234.56" / "61.71" → decimal (estes layouts usam ponto decimal).</summary>
    private static decimal Dot(string s)
        => decimal.Parse(s.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static int? ToCents(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : (int)Math.Round(Dot(s) * 100m, MidpointRounding.AwayFromZero);
}
