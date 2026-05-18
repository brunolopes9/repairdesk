namespace RepairDesk.Common.Helpers;

/// <summary>
/// Validador de NIF (Número de Identificação Fiscal) português.
/// Algoritmo oficial AT: 9 dígitos numéricos, último é check-digit (mod 11 ponderado).
/// </summary>
/// <remarks>
/// Primeiro dígito (ou primeiros 2) indica categoria:
///   1, 2, 3        — pessoa singular
///   45             — não-residente
///   5              — pessoa colectiva
///   6              — administração pública
///   70, 74, 75     — herança indivisa
///   71             — não-residente colectivo
///   77             — sociedade civil sem personalidade jurídica
///   79             — regime especial
///   8              — empresário individual (descontinuado)
///   9              — pessoa colectiva irregular / provisório
///
/// Check-digit:
///   sum = sum(digit[i] * weight[i]) for i in 0..7, weights = [9,8,7,6,5,4,3,2]
///   r = sum mod 11
///   check = r &lt; 2 ? 0 : 11 - r
///
/// Espelha frontend em frontend/src/lib/nif/validator.ts para defesa em profundidade.
/// </remarks>
public static class NifValidator
{
    private static readonly char[] ValidFirstDigits = ['1', '2', '3', '5', '6', '8', '9'];
    private static readonly string[] ValidFirstTwo = ["45", "70", "71", "72", "74", "75", "77", "79"];

    public static string Normalize(string? nif) =>
        new string((nif ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// True se o NIF é válido: 9 dígitos, primeiro dígito de categoria válida,
    /// check-digit conforme algoritmo AT.
    /// </summary>
    public static bool IsValid(string? nif)
    {
        var clean = Normalize(nif);
        if (clean.Length != 9) return false;

        var firstTwo = clean[..2];
        var firstDigitValid =
            Array.IndexOf(ValidFirstDigits, clean[0]) >= 0
            || Array.IndexOf(ValidFirstTwo, firstTwo) >= 0;
        if (!firstDigitValid) return false;

        var weights = new[] { 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            sum += (clean[i] - '0') * weights[i];
        }
        var remainder = sum % 11;
        var expected = remainder < 2 ? 0 : 11 - remainder;
        var actual = clean[8] - '0';
        return expected == actual;
    }
}
