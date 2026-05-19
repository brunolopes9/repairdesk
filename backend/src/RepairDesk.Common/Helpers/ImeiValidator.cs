namespace RepairDesk.Common.Helpers;

/// <summary>
/// Validador de IMEI (International Mobile Equipment Identity).
/// Aplica o algoritmo Luhn standard para verificação do dígito de controlo.
/// </summary>
/// <remarks>
/// Formatos aceites:
/// - 15 dígitos: IMEI standard (mais comum, GSM/3G/4G/5G)
/// - 16 dígitos: IMEISV (com software version)
/// - 14 dígitos: IMEI legacy (versões antigas, validação opcional)
///
/// Limpa espaços, hífens e pontos antes da validação. Dispositivos Apple
/// reportam IMEI nos mesmos formatos.
/// </remarks>
public static class ImeiValidator
{
    public static string Normalize(string? imei) =>
        new string((imei ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Verifica se o IMEI tem comprimento aceitável (14, 15 ou 16 dígitos)
    /// e check-digit Luhn válido (para 15 dígitos é obrigatório; para 14/16 é
    /// opcional — devolve true se passar Luhn ou se length=14/16).
    /// </summary>
    public static bool IsValid(string? imei)
    {
        var clean = Normalize(imei);
        if (clean.Length is < 14 or > 16) return false;
        if (clean.Length == 15) return LuhnCheck(clean);
        // IMEI 14 (legacy) e IMEISV 16 não usam Luhn padronizado — aceitar
        return true;
    }

    /// <summary>
    /// Mascarado para audit logs e UI quando IMEI não deve ser exposto inteiro.
    /// Mantém os primeiros 3 dígitos (TAC manufacturer) e os últimos 4, mascara o meio.
    /// </summary>
    public static string Mask(string? imei)
    {
        var clean = Normalize(imei);
        if (clean.Length < 8) return "***";
        return clean[..3] + new string('X', clean.Length - 7) + clean[^4..];
    }

    /// <summary>Aplica algoritmo Luhn a uma string de dígitos.</summary>
    public static bool LuhnCheck(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return false;
        var sum = 0;
        var alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (d is < 0 or > 9) return false;
            if (alternate)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }
}
