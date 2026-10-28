using System.Text.RegularExpressions;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Fornecedores;

/// <summary>
/// Sprint 579: o NIF / nº de IVA como identidade do fornecedor. O nome numa fatura varia ("Hetzner",
/// "Hetzner Online GmbH", "LCPhones", "POCINHAS SARL") — o NIF do emitente não. Comparações fazem-se
/// pela <see cref="Chave"/> (sem o prefixo do país), porque o mesmo NIF aparece como "PT500000000" numa
/// fatura e "500000000" noutra.
/// </summary>
public static partial class NifFornecedor
{
    /// <summary>Maiúsculas e só letras/dígitos ("de 811 234 567" → "DE811234567"). Null se vazio.</summary>
    public static string? Normalizar(string? nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        var limpo = new string(nif.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>NIF sem o prefixo do país ("PT263758141" e "263758141" → "263758141").</summary>
    public static string? Chave(string? nif)
    {
        var n = Normalizar(nif);
        if (n is null) return null;
        return n.Length > 2 && char.IsAsciiLetter(n[0]) && char.IsAsciiLetter(n[1]) && n[2..].Any(char.IsAsciiDigit) ? n[2..] : n;
    }

    /// <summary>Formato plausível de NIF/VAT: 8 a 14 caracteres com pelo menos 6 dígitos.</summary>
    public static bool Plausivel(string? nif)
    {
        var n = Normalizar(nif);
        return n is { Length: >= 8 and <= 14 } && n.Count(char.IsAsciiDigit) >= 6;
    }

    /// <summary>
    /// O fornecedor conhecido cujo NIF aparece no texto da fatura (o NIF do próprio negócio — o do
    /// cliente na fatura — é ignorado). Null se nenhum ou se mais do que um (ambíguo: decide o nome).
    /// </summary>
    public static Fornecedor? Encontrar(IEnumerable<Fornecedor> conhecidos, string? texto, string? nifProprio)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var proprio = Chave(nifProprio);
        // Duas leituras do texto: tal como está (sem pontos/hífens) e com os grupos de dígitos colados
        // ("DE 811 234 567" → "DE811234567"). Em ambas o NIF tem de estar entre não-dígitos.
        var a = PontuacaoRegex().Replace(texto.ToUpperInvariant(), "");
        var b = EspacoEntreDigitosRegex().Replace(a, "");
        var encontrados = conhecidos
            .Where(f => Chave(f.Nif) is { Length: >= 8 } k && k != proprio && (Contem(a, k) || Contem(b, k)))
            .DistinctBy(f => f.Id)
            .ToList();
        return encontrados.Count == 1 ? encontrados[0] : null;
    }

    /// <summary>
    /// O NIF do emitente lido do texto da fatura: nºs de IVA com prefixo de país UE e NIFs portugueses
    /// válidos junto a "NIF"/"Contribuinte"/"VAT". Exclui o NIF do próprio negócio. Só devolve quando há
    /// exatamente um candidato — com dois (ex.: Amazon + vendedor) não adivinha.
    /// </summary>
    public static string? DoEmitente(string? texto, string? nifProprio)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var proprio = Chave(nifProprio);
        var candidatos = new List<string>();
        foreach (Match m in VatUeRegex().Matches(texto))
        {
            var nif = Normalizar(m.Value);
            if (Plausivel(nif)) candidatos.Add(nif!);
        }
        foreach (Match m in NifPtRotuladoRegex().Matches(texto))
            if (NifValidator.IsValid(m.Groups["nif"].Value)) candidatos.Add("PT" + m.Groups["nif"].Value);
        var unicos = candidatos.Where(c => Chave(c) != proprio).DistinctBy(Chave).ToList();
        return unicos.Count == 1 ? unicos[0] : null;
    }

    private static bool Contem(string texto, string chave)
    {
        for (var i = texto.IndexOf(chave, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(chave, i + 1, StringComparison.Ordinal))
        {
            var antes = i == 0 || !char.IsAsciiDigit(texto[i - 1]);
            var fim = i + chave.Length;
            var depois = fim >= texto.Length || !char.IsAsciiDigit(texto[fim]);
            if (antes && depois) return true;
        }
        return false;
    }

    [GeneratedRegex(@"[.\-]")]
    private static partial Regex PontuacaoRegex();

    [GeneratedRegex(@"(?<=\d) (?=\d)")]
    private static partial Regex EspacoEntreDigitosRegex();

    // Prefixo de país UE (+ GB/XI) seguido de 8–12 caracteres (dígitos e maiúsculas, espaços opcionais).
    [GeneratedRegex(@"\b(?:AT|BE|BG|CY|CZ|DE|DK|EE|EL|ES|FI|FR|HR|HU|IE|IT|LT|LU|LV|MT|NL|PL|PT|RO|SE|SI|SK|GB|XI)[ ]?(?:[0-9A-Z][ ]?){7,11}[0-9A-Z]\b")]
    private static partial Regex VatUeRegex();

    [GeneratedRegex(@"(?:NIF|NIPC|Contribuinte|VAT(?:\s*(?:ID|No\.?|Number|Nr\.?))?|Tax\s*ID)\s*[:.º°]?\s*(?:PT)?\s*(?<nif>\d{9})\b", RegexOptions.IgnoreCase)]
    private static partial Regex NifPtRotuladoRegex();
}
