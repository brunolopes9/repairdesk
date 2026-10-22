using System.Globalization;
using System.Text.RegularExpressions;

namespace RepairDesk.Services.Vendas;

/// <summary>Linha de uma fatura de venda: preço unitário SEM IVA (como vem impresso) e taxa (0 = isento).</summary>
public sealed record FaturaVendaLinha(string Descricao, decimal Quantidade, int PrecoUnitarioSemIvaCents, decimal IvaRate);

public sealed record FaturaVendaLida(
    string Numero,
    DateTime Data,
    string ClienteNome,
    string? ClienteNif,
    string? ClienteMorada,
    IReadOnlyList<FaturaVendaLinha> Linhas,
    int TotalCents,
    /// <summary>Motivo de isenção impresso (ex.: "M40 - IVA - Autoliquidação"), se houver.</summary>
    string? MotivoIsencao)
{
    public bool ConsumidorFinal => ClienteNif is null && ClienteNome.Equals("Consumidor final", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Sprint 565: lê as faturas emitidas no Moloni ("Powered by Moloni") — só a via "Original" (o PDF traz
/// também o "Duplicado"). Determinístico: se faltar nº, data, cliente ou as linhas não somarem o total
/// ilíquido, devolve null em vez de inventar.
/// </summary>
public static class MoloniFaturaParser
{
    private static readonly Regex Numero = new(@"^Fatura\s+(FT\s+\S+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Data = new(@"Data de emiss[ãa]o:\s*(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled);
    private static readonly Regex LinhaFim = new(
        @"^(?<desc>.*?)\s*(?<qtd>\d+(?:,\d+)?)\s+(?:un|h|uni|kg|m)\.?\s+(?<preco>[\d.]*\d,\d{2})€\s+(?<iva>\d+(?:,\d+)?%|Isento(?:\s*\(\d+\))?)\s+(?<tot>[\d.]*\d,\d{2})€$",
        RegexOptions.Compiled);
    private static readonly Regex NifCliente = new(@"^(?<nif>[A-Z]{0,2}\d{6,})\s+\d+\s+EUR\b", RegexOptions.Compiled);
    private static readonly Regex InicioMorada = new(@"\d|^(rua|r\.|av|avenida|travessa|largo|estrada|praça|praca|urb|bairro|lugar|quinta|calle)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool Matches(string text) => text.Contains("Moloni", StringComparison.OrdinalIgnoreCase) && Numero.IsMatch(text);

    public static FaturaVendaLida? Parse(string text)
    {
        var original = SoOriginal(text);
        var linhas = original.Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        var numero = Numero.Match(original) is { Success: true } n ? Regex.Replace(n.Groups[1].Value, @"\s+", " ") : null;
        var data = Data.Match(original) is { Success: true } d
            && DateTime.TryParseExact(d.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : (DateTime?)null;
        if (numero is null || data is null) return null;

        // Bloco do cliente: entre o NIF do emitente ("NIF: …") e o "ATCUD"; a última linha é o país.
        var iNif = linhas.FindIndex(l => l.StartsWith("NIF:", StringComparison.OrdinalIgnoreCase));
        var iAtcud = linhas.FindIndex(l => l.StartsWith("ATCUD", StringComparison.OrdinalIgnoreCase));
        if (iNif < 0 || iAtcud <= iNif + 1) return null;
        var bloco = linhas.Skip(iNif + 1).Take(iAtcud - iNif - 1).ToList();
        if (bloco.Count > 1) bloco.RemoveAt(bloco.Count - 1); // país
        var iMorada = bloco.FindIndex(1, l => InicioMorada.IsMatch(l));
        var nomeLinhas = iMorada < 0 ? bloco : bloco.Take(iMorada).ToList();
        var nome = Regex.Replace(string.Join(' ', nomeLinhas), @"\s+", " ").Trim();
        var morada = iMorada < 0 ? null : string.Join(", ", bloco.Skip(iMorada));
        if (nome.Length == 0) return null;

        var nif = linhas.Select(l => NifCliente.Match(l)).FirstOrDefault(m => m.Success)?.Groups["nif"].Value;

        // Linhas: da cabeça "Ref. Designação" até "Total ilíquido"; uma descrição pode ocupar várias linhas.
        var iCab = linhas.FindIndex(l => l.StartsWith("Ref.", StringComparison.OrdinalIgnoreCase) && l.Contains("Designa", StringComparison.OrdinalIgnoreCase));
        var iFim = linhas.FindIndex(l => l.StartsWith("Total ilíquido", StringComparison.OrdinalIgnoreCase));
        if (iCab < 0 || iFim <= iCab) return null;
        var itens = new List<FaturaVendaLinha>();
        var buffer = new List<string>();
        foreach (var l in linhas.Skip(iCab + 1).Take(iFim - iCab - 1))
        {
            var m = LinhaFim.Match(l);
            if (!m.Success) { buffer.Add(l); continue; }
            buffer.Add(m.Groups["desc"].Value);
            var desc = Regex.Replace(string.Join(' ', buffer.Where(b => b.Length > 0)), @"\s+", " ").Trim();
            buffer.Clear();
            var iva = m.Groups["iva"].Value.StartsWith("Isento", StringComparison.OrdinalIgnoreCase)
                ? 0m : decimal.Parse(m.Groups["iva"].Value.TrimEnd('%').Replace(',', '.'), CultureInfo.InvariantCulture);
            itens.Add(new FaturaVendaLinha(desc.Length == 0 ? "Serviço" : desc,
                decimal.Parse(m.Groups["qtd"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                Cents(m.Groups["preco"].Value), iva));
        }
        if (itens.Count == 0) return null;

        var totalIliquido = Cents(Regex.Match(linhas[iFim], @"([\d.]*\d,\d{2})€").Groups[1].Value);
        var somaLinhas = itens.Sum(i => (int)Math.Round(i.PrecoUnitarioSemIvaCents * i.Quantidade, MidpointRounding.AwayFromZero));
        if (Math.Abs(somaLinhas - totalIliquido) > 1) return null;

        var total = Regex.Match(original, @"Total a pagar\s*([\d.]*\d,\d{2})€") is { Success: true } t ? Cents(t.Groups[1].Value) : (int?)null;
        if (total is null) return null;
        var motivo = Regex.Match(original, @"\(\d+\)\s*(M\d{2}\s*-[^\n]*?)\s+-\s+[\d.]*\d,\d{2}€") is { Success: true } mo ? mo.Groups[1].Value.Trim() : null;

        return new FaturaVendaLida(numero, data.Value, nome, nif, morada, itens, total.Value, motivo);
    }

    /// <summary>O PDF do Moloni traz "Original" e "Duplicado" — fica só com o primeiro.</summary>
    private static string SoOriginal(string text)
    {
        var i = text.IndexOf("\nDuplicado", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? text[..i] : text;
    }

    private static int Cents(string s)
        => (int)Math.Round(decimal.Parse(s.Replace(".", string.Empty).Replace(',', '.'), CultureInfo.InvariantCulture) * 100m, MidpointRounding.AwayFromZero);
}
