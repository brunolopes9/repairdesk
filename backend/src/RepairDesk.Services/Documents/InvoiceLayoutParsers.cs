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
        if (SpainSellers.Matches(text)) return SpainSellers.Parse(text);
        if (InterMovil.Matches(text)) return InterMovil.Parse(text);
        if (FourPhones.Matches(text)) return FourPhones.Parse(text);
        if (IPartsTrade.Matches(text)) return IPartsTrade.Parse(text);
        if (Microwire.Matches(text)) return Microwire.Parse(text);
        if (Utopya.Matches(text)) return Utopya.Parse(text);
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

    /// <summary>
    /// Faturas da SpainSellers (Microrepuestos S.L.U., ES) — entrega intracomunitária, normalmente 0% IVA.
    /// O extrator cola a quantidade ao preço: <c>2000000835150 BATERÍA … 5,402 5,40 10,80</c> = PVP 5,40 ×
    /// 2 un., PVP c/ IVA 5,40, subtotal 10,80 (cada linha é conferida: preço × qtd = subtotal). Valores com
    /// vírgula; portes e descontos com ponto. O desconto global é repartido pelos artigos em proporção, para
    /// o custo de cada lote já refletir o que se pagou.
    /// </summary>
    public static class SpainSellers
    {
        private static readonly Regex ItemLine = new(
            @"^(?<code>\d{8,})\s+(?<desc>.+?)\s+(?<pvp>[\d.]*\d,\d{2})(?<uds>\d+)\s+(?<pvpiva>[\d.]*\d,\d{2})\s+(?<sub>[\d.]*\d,\d{2})$",
            RegexOptions.Compiled);

        private static readonly Regex Totais = new(
            @"^(?<base>[\d.]*\d,\d{2})\s+(?<iva>[\d.]*\d,\d{2})\s+(?<total>[\d.]*\d,\d{2})$",
            RegexOptions.Compiled);

        public static bool Matches(string text)
            => text.Contains("spainsellers", StringComparison.OrdinalIgnoreCase)
               && Regex.IsMatch(text, @"FACTURA\s*:", RegexOptions.IgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var numero = Match(text, @"FACTURA\s*:\s*(\d+/\d+)");
            var dataTxt = Match(text, @"FECHA:\s*(\d{2}/\d{2}/\d{4})") ?? Match(text, @"(\d{2}/\d{2}/\d{4})\s*FACTURA");
            DateTime? data = DateTime.TryParseExact(dataTxt, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
                : null;
            var encomenda = Match(text, @"REFERENCIA WEB:\s*(\d+)");
            var portes = ToCents(Match(text, @"PORTES\s+(-?\d+(?:\.\d+)?)")) ?? 0;
            var desconto = Math.Abs(ToCents(Match(text, @"DESCUENTOS\s+(-?\d+(?:\.\d+)?)")) ?? 0);

            var linhas = new List<(string Desc, int Qtd, int Cents)>();
            var linhasConferem = true;
            int? total = null;
            var ivaZero = true;
            foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                var m = ItemLine.Match(line);
                if (m.Success)
                {
                    var qtd = int.Parse(m.Groups["uds"].Value, CultureInfo.InvariantCulture);
                    var unitIva = Virgula(m.Groups["pvpiva"].Value);
                    var sub = Virgula(m.Groups["sub"].Value);
                    linhasConferem &= qtd > 0 && Math.Abs(unitIva * qtd - sub) <= 2;
                    linhas.Add((m.Groups["desc"].Value.Trim(), qtd, (int)(unitIva * qtd)));
                    continue;
                }
                var t = Totais.Match(line);
                if (t.Success && total is null)
                {
                    total = Virgula(t.Groups["total"].Value);
                    ivaZero = Virgula(t.Groups["iva"].Value) == 0;
                }
            }

            // Desconto global repartido pelos artigos em proporção (o último fica com o arredondamento).
            var bruto = linhas.Sum(l => l.Cents);
            var items = new List<SupplierPdfItem>();
            var restante = desconto;
            for (var i = 0; i < linhas.Count; i++)
            {
                var l = linhas[i];
                var parte = i == linhas.Count - 1 ? restante : bruto == 0 ? 0 : (int)Math.Round((decimal)desconto * l.Cents / bruto, MidpointRounding.AwayFromZero);
                restante -= parte;
                items.Add(new SupplierPdfItem(l.Desc, l.Qtd, l.Cents - parte));
            }
            if (portes > 0) items.Add(new SupplierPdfItem("Portes", 1, portes));

            var reconcilia = total is { } tot && items.Count > 0 && Math.Abs(items.Sum(i => i.LineTotalCents) - tot) <= 2;
            // IVA ≠ 0 (ex.: venda nacional espanhola) não é o caso normal — vai a revisão.
            var confianca = numero is not null && data is not null && reconcilia && linhasConferem && ivaZero && desconto <= bruto
                ? ParseConfidence.High
                : ParseConfidence.Low;
            return new SupplierPdfParseResult("SpainSellers", numero, total, data, confianca, items, OrderReference: encomenda);
        }

        /// <summary>"1.234,56" → cêntimos.</summary>
        private static int Virgula(string s)
            => (int)Math.Round(decimal.Parse(s.Replace(".", string.Empty).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture) * 100m, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Faturas da Inter Movil Levante SL (Movil Flash, ES). Cabeçalho "007962 1 13/10/2026" (nº, página,
    /// data); cada artigo começa por um código ("22082 Embellecedor …") e a descrição pode continuar na linha
    /// seguinte, que termina com "qtd preço subtotal total". Resumo: "importe portes base" (sem IVA = intra-UE).
    /// </summary>
    public static class InterMovil
    {
        // O extrator junta-o à linha anterior ("Viseu 007962 1 13/10/2026").
        private static readonly Regex Cabecalho = new(@"(?:^|\s)(?<num>\d{4,8})\s+\d+\s+(?<data>\d{2}/\d{2}/\d{4})$", RegexOptions.Compiled);
        private static readonly Regex InicioArtigo = new(@"^(?<code>\d{3,8})\s+(?<desc>\D.*)$", RegexOptions.Compiled);
        private static readonly Regex Cauda = new(
            @"^(?<desc>.*?)\s*(?<qtd>\d+,\d{2})\s+(?<preco>[\d.]*\d,\d{2})\s+(?<sub>[\d.]*\d,\d{2})\s+(?<total>[\d.]*\d,\d{2})$",
            RegexOptions.Compiled);
        private static readonly Regex Resumo = new(@"^(?<importe>[\d.]*\d,\d{2})\s+(?<portes>[\d.]*\d,\d{2})\s+(?<base>[\d.]*\d,\d{2})$", RegexOptions.Compiled);

        public static bool Matches(string text)
            => text.Contains("Inter Movil Levante", StringComparison.OrdinalIgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            string? numero = null;
            DateTime? data = null;
            var items = new List<SupplierPdfItem>();
            string? descAberta = null;
            int? importe = null, portes = null, baseTributavel = null;
            foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (numero is null && Cabecalho.Match(line) is { Success: true } h)
                {
                    numero = h.Groups["num"].Value;
                    if (DateTime.TryParseExact(h.Groups["data"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        data = DateTime.SpecifyKind(d, DateTimeKind.Utc);
                    continue;
                }
                var cauda = Cauda.Match(line);
                if (cauda.Success)
                {
                    // Com descrição já aberta, a cauda é continuação (mesmo que comece por números, ex.: "11 4G …").
                    var inicio = descAberta is null ? InicioArtigo.Match(cauda.Groups["desc"].Value) : System.Text.RegularExpressions.Match.Empty;
                    var desc = inicio.Success ? inicio.Groups["desc"].Value : $"{descAberta} {cauda.Groups["desc"].Value}";
                    var qtd = Virgula(cauda.Groups["qtd"].Value) / 100;
                    items.Add(new SupplierPdfItem(desc.Trim(), Math.Max(1, qtd), Virgula(cauda.Groups["total"].Value)));
                    descAberta = null;
                    continue;
                }
                if (InicioArtigo.Match(line) is { Success: true } a && numero is not null && items.Count < 200)
                {
                    descAberta = a.Groups["desc"].Value;
                    continue;
                }
                if (Resumo.Match(line) is { Success: true } r)
                {
                    importe = Virgula(r.Groups["importe"].Value);
                    portes = Virgula(r.Groups["portes"].Value);
                    baseTributavel = Virgula(r.Groups["base"].Value);
                }
            }
            if (portes is > 0) items.Add(new SupplierPdfItem("Portes", 1, portes.Value));

            var total = baseTributavel;
            var reconcilia = total is { } t && items.Count > 0 && Math.Abs(items.Sum(i => i.LineTotalCents) - t) <= 2
                && importe + portes == baseTributavel;
            var confianca = numero is not null && data is not null && reconcilia ? ParseConfidence.High : ParseConfidence.Low;
            return new SupplierPdfParseResult("Inter Movil Levante", numero, total, data, confianca, items);
        }

        private static int Virgula(string s)
            => (int)Math.Round(decimal.Parse(s.Replace(".", string.Empty).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture) * 100m, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Faturas da 4Phones.nl (NL, autoliquidação). "Invoicenumber: INV-NL-… Ordernumber: 4EU…/SO-…";
    /// artigos com descrição em várias linhas (a 1.ª começa pelo código) e a linha "1 pc € 11.95 0% € 11.95".
    /// O cabeçalho repete-se em cada página — a descrição em curso é descartada nessas linhas.
    /// </summary>
    public static class FourPhones
    {
        private static readonly Regex Qtd = new(@"^(?<qtd>\d+)\s+pcs?\s+€\s*(?<unit>[\d.,]+)\s+(?<iva>\d+)%\s+€\s*(?<tot>[\d.,]+)$", RegexOptions.Compiled);

        public static bool Matches(string text) => text.Contains("4Phones.nl", StringComparison.OrdinalIgnoreCase) && text.Contains("Invoicenumber", StringComparison.OrdinalIgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var numero = Match(text, @"Invoicenumber:?\s*(INV-[A-Z]{2}-\d+)");
            var encomenda = Match(text, @"Ordernumber:?\s*(4EU\d+)");
            var data = DataDmy(Match(text, @"Invoicedate:?\s*(\d{2}/\d{2}/\d{4})"));
            var items = new List<SupplierPdfItem>();
            var buffer = new List<string>();
            var dentro = false;
            var ivaZero = true;
            foreach (var raw in Linhas(text))
            {
                if (raw.StartsWith("Article Description", StringComparison.OrdinalIgnoreCase)) { dentro = true; buffer.Clear(); continue; }
                if (raw.StartsWith("4Phones.nl", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("Subtotal", StringComparison.OrdinalIgnoreCase)) { dentro = false; buffer.Clear(); continue; }
                if (!dentro) continue;
                var m = Qtd.Match(raw);
                if (!m.Success) { buffer.Add(raw); continue; }
                ivaZero &= m.Groups["iva"].Value == "0";
                items.Add(new SupplierPdfItem(SemCodigoInicial(buffer), int.Parse(m.Groups["qtd"].Value, CultureInfo.InvariantCulture), ToCents(m.Groups["tot"].Value) ?? 0));
                buffer.Clear();
            }
            var portes = ToCents(Match(text, @"^Shipping\s+€\s*([\d.,]+)")) ?? 0;
            if (portes > 0) items.Add(new SupplierPdfItem("Shipping", 1, portes));
            var total = ToCents(Match(text, @"^Total amount\s+€\s*([\d.,]+)"));
            return Resultado("4Phones", numero, total, data, items, ivaZero, encomenda);
        }
    }

    /// <summary>
    /// Faturas da iPartsTrade (NL, autoliquidação). Atenção: "Date Added 09/03/2026" é mês/dia/ano (3 de
    /// setembro). Artigos: 1.ª linha começa pelo código; a linha que fecha termina em "qtd €preço €total".
    /// Portes = total − artigos (vêm numa linha partida "Fedex … €10.95").
    /// </summary>
    public static class IPartsTrade
    {
        private static readonly Regex Cauda = new(@"^(?<desc>.*?)\s*(?<qtd>\d+)\s+€(?<unit>[\d.,]+)\s+€(?<tot>[\d.,]+)$", RegexOptions.Compiled);

        public static bool Matches(string text) => text.Contains("iPartsTrade", StringComparison.OrdinalIgnoreCase) && text.Contains("Invoice No", StringComparison.OrdinalIgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var numero = Match(text, @"Invoice No\.?\s*(I\d{4}-\d+)");
            var encomenda = Match(text, @"Order ID\s*(\d+)");
            var dataTxt = Match(text, @"Date Added\s*(\d{2}/\d{2}/\d{4})");
            DateTime? data = DateTime.TryParseExact(dataTxt, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
            var items = new List<SupplierPdfItem>();
            var buffer = new List<string>();
            var dentro = false;
            foreach (var raw in Linhas(text))
            {
                if (raw.StartsWith("Model Product Name", StringComparison.OrdinalIgnoreCase)) { dentro = true; continue; }
                if (raw.StartsWith("Total excl", StringComparison.OrdinalIgnoreCase)) break;
                if (!dentro) continue;
                var m = Cauda.Match(raw);
                if (!m.Success) { buffer.Add(raw); continue; }
                buffer.Add(m.Groups["desc"].Value);
                items.Add(new SupplierPdfItem(SemCodigoInicial(buffer), int.Parse(m.Groups["qtd"].Value, CultureInfo.InvariantCulture), ToCents(m.Groups["tot"].Value) ?? 0));
                buffer.Clear();
            }
            var total = ToCents(Match(text, @"Total incl\.? VAT\s*€\s*([\d.,]+)"));
            var ivaZero = Regex.IsMatch(text, @"exempt\s*€0[.,]00", RegexOptions.IgnoreCase);
            var portes = total is { } t ? t - items.Sum(i => i.LineTotalCents) : 0;
            if (portes > 0) items.Add(new SupplierPdfItem("Shipping", 1, portes));
            return Resultado("iPartsTrade", numero, total, data, items, ivaZero && portes >= 0, encomenda);
        }
    }

    /// <summary>
    /// Faturas da Microwire (Clouds and Roots, PT — software Primavera). "Fatura recibo FCTR FCTR2026A/4031";
    /// linha de artigo "… 1 UN 12,36 0% 23% 15,21" (preço s/ IVA, total da linha c/ IVA). Os portes vêm como
    /// artigo "PORTES …".
    /// </summary>
    public static class Microwire
    {
        private static readonly Regex Cauda = new(@"^(?<desc>.*?)\s*(?<qtd>\d+)\s+UN\s+(?<unit>[\d.]*\d,\d{2})\s+\d+%\s+(?<iva>\d+)%\s+(?<tot>[\d.]*\d,\d{2})$", RegexOptions.Compiled);

        public static bool Matches(string text) => text.Contains("CLOUDS AND ROOTS", StringComparison.OrdinalIgnoreCase) || text.Contains("microwire.pt", StringComparison.OrdinalIgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var numero = Match(text, @"Fatura(?:\s+recibo)?\s+[A-Z]+\s+(FCTR\S+)");
            var encomenda = Match(text, @"MW[A-Z]{2}\s*#\s*(\d+)");
            var data = DataDmy(Match(text, @"^Data\s*\n\s*(\d{2}/\d{2}/\d{4})"));
            var items = new List<SupplierPdfItem>();
            var buffer = new List<string>();
            var dentro = false;
            foreach (var raw in Linhas(text))
            {
                if (raw.StartsWith("Artigo Descri", StringComparison.OrdinalIgnoreCase)) { dentro = true; continue; }
                if (raw.StartsWith("Produtos e Servi", StringComparison.OrdinalIgnoreCase)) break;
                if (!dentro || raw.Contains("Processado por programa", StringComparison.OrdinalIgnoreCase)) continue;
                var m = Cauda.Match(raw);
                if (!m.Success) { buffer.Add(raw); continue; }
                buffer.Add(m.Groups["desc"].Value);
                items.Add(new SupplierPdfItem(SemCodigoInicial(buffer), int.Parse(m.Groups["qtd"].Value, CultureInfo.InvariantCulture), Virgula(m.Groups["tot"].Value)));
                buffer.Clear();
            }
            var total = Match(text, @"^Total\s+([\d.]*\d,\d{2})\s*€") is { } t ? Virgula(t) : (int?)null;
            return Resultado("Microwire", numero, total, data, items, ivaZero: true, encomenda);
        }
    }

    /// <summary>
    /// Faturas da Utopya (FR, autoliquidação): "Invoice # INV-1024600 - Order # UE027541"; artigos
    /// "918273 Complete Screen … 1 29,63€ 29,63€0%*"; portes "Shipping cost UPS Standard 1 6,95€ 6,95€0%*".
    /// </summary>
    public static class Utopya
    {
        private static readonly Regex Artigo = new(@"^(?<desc>.+?)\s+(?<qtd>\d+)\s+(?<unit>[\d.]*\d,\d{2})€\s+(?<sub>[\d.]*\d,\d{2})€\s*(?<iva>\d+)%", RegexOptions.Compiled);

        public static bool Matches(string text) => text.Contains("UTOPYA", StringComparison.Ordinal) && Regex.IsMatch(text, @"Invoice\s*#\s*INV-", RegexOptions.IgnoreCase);

        public static SupplierPdfParseResult Parse(string text)
        {
            var numero = Match(text, @"Invoice\s*#\s*(INV-\d+)");
            var encomenda = Match(text, @"Order\s*#\s*(UE\d+)");
            var data = DataDmy(Match(text, @"^\D*?(\d{2}/\d{2}/\d{4})\s+\d{2}/\d{2}/\d{4}"));
            var items = new List<SupplierPdfItem>();
            var ivaZero = true;
            foreach (var raw in Linhas(text))
            {
                var m = Artigo.Match(raw);
                if (!m.Success) continue;
                ivaZero &= m.Groups["iva"].Value == "0";
                var desc = Regex.Replace(m.Groups["desc"].Value, @"^\d{4,}\s+", string.Empty);
                items.Add(new SupplierPdfItem(desc, int.Parse(m.Groups["qtd"].Value, CultureInfo.InvariantCulture), Virgula(m.Groups["sub"].Value)));
            }
            var total = Match(text, @"^Total\s+([\d.]*\d,\d{2})€") is { } t ? Virgula(t) : (int?)null;
            return Resultado("Utopya", numero, total, data, items, ivaZero, encomenda);
        }
    }

    // ---------- helpers partilhados pelos leitores acima ----------

    private static IEnumerable<string> Linhas(string text)
        => text.Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    private static DateTime? DataDmy(string? s)
        => DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;

    /// <summary>Junta a descrição de várias linhas e tira o código de artigo do início ("A00003054 Kingston…").</summary>
    private static string SemCodigoInicial(List<string> linhas)
    {
        var desc = string.Join(' ', linhas.Where(l => l.Length > 0)).Trim();
        // Código = 1.º token só com maiúsculas/dígitos/hífen e pelo menos um dígito (não apaga "For", "Xiaomi"…).
        return Regex.Replace(desc, @"^(?=\S*\d)[A-Z0-9][A-Z0-9-]{2,}\s+(?:[A-Z0-9-]{2,}\s+)?(?=\S)", string.Empty).Trim();
    }

    /// <summary>"1.234,56" → cêntimos.</summary>
    private static int Virgula(string s)
        => (int)Math.Round(decimal.Parse(s.Replace(".", string.Empty).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture) * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Confiança alta só com nº, data, IVA esperado e linhas a somar o total impresso.</summary>
    private static SupplierPdfParseResult Resultado(string fornecedor, string? numero, int? total, DateTime? data,
        List<SupplierPdfItem> items, bool ivaZero, string? encomenda)
    {
        var reconcilia = total is { } t && items.Count > 0 && Math.Abs(items.Sum(i => i.LineTotalCents) - t) <= 2;
        var confianca = numero is not null && data is not null && reconcilia && ivaZero ? ParseConfidence.High : ParseConfidence.Low;
        return new SupplierPdfParseResult(fornecedor, numero, total, data, confianca, items, OrderReference: encomenda);
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
