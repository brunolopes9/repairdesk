namespace RepairDesk.Common.Helpers;

/// <summary>
/// Sprint 248 (Doc 73 Fase C): normaliza nomes de ficheiros enviados pelo cliente para
/// armazenamento seguro. Promovido para Common a partir do SafeFileName local de
/// FotoService.
///
/// Garantias:
/// - Nunca devolve path com separadores (<c>/</c>, <c>\</c>) — `Path.GetFileName` strip
///   directórios antes de filtrar.
/// - Remove caracteres que possam causar problemas em filesystems ou logs.
/// - Trunca a um limite seguro (default 100 chars).
/// - Devolve <c>"file"</c> quando o nome ficar vazio após normalização (vs string vazia
///   que pode partir consumers que assumem não-null/não-vazio).
/// </summary>
public static class FileNameSanitizer
{
    public const int DefaultMaxLength = 100;

    /// <summary>
    /// Devolve uma versão sanitizada de <paramref name="raw"/>. Strip de path traversal,
    /// caracteres especiais, e truncamento. Nunca lança — input null/empty devolve
    /// <c>"file"</c>.
    /// </summary>
    public static string Safe(string? raw, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "file";

        // Remove componentes de path — defesa contra "../../etc/passwd" e variantes Windows.
        // Path.GetFileName em Linux só trata '/' como separator; normalizar '\' → '/' garante
        // que paths Windows também são strippados quando o backend corre em Linux.
        var normalized = raw.Replace('\\', '/');
        string nameOnly;
        try { nameOnly = Path.GetFileName(normalized); }
        catch { nameOnly = normalized; }
        if (string.IsNullOrWhiteSpace(nameOnly)) nameOnly = normalized;

        // Whitelist conservadora: letras/dígitos + . - _ e espaço. Tudo o resto cai.
        var clean = string.Concat(nameOnly.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' '));

        // Aparar espaços e pontos no início/fim (Windows não aceita).
        clean = clean.Trim(' ', '.');

        // Bloquear nomes reservados Windows (CON, PRN, AUX, NUL, COM1-9, LPT1-9) — mesmo
        // que o backend corra Linux, o ficheiro pode ser baixado em Windows.
        var stem = Path.GetFileNameWithoutExtension(clean).ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.StartsWith("COM", StringComparison.Ordinal) && stem.Length == 4 && char.IsDigit(stem[3]))
            || (stem.StartsWith("LPT", StringComparison.Ordinal) && stem.Length == 4 && char.IsDigit(stem[3])))
        {
            clean = "_" + clean;
        }

        if (clean.Length == 0) return "file";
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }
}
