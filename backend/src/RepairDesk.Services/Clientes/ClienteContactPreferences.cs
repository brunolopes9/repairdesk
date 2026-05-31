namespace RepairDesk.Services.Clientes;

internal static class ClienteContactPreferences
{
    private static readonly Dictionary<string, string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["telefone"] = "Telefone",
        ["phone"] = "Telefone",
        ["whatsapp"] = "WhatsApp",
        ["email"] = "Email",
        ["sms"] = "Sms",
    };

    public static bool IsValidChannel(string? value)
        => string.IsNullOrWhiteSpace(value) || NormalizeChannel(value) is not null;

    public static string? NormalizeChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim();
        return Channels.TryGetValue(key, out var normalized) ? normalized : null;
    }

    public static bool ParseCsvBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToLowerInvariant();
        return v is "1" or "true" or "sim" or "yes" or "y";
    }
}
