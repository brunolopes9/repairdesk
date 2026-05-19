using System.Security.Cryptography;
using System.Text;

namespace RepairDesk.Common.Helpers;

/// <summary>
/// Geração + hashing de service API keys.
/// Formato: <c>rd_live_</c> + 32 chars alfanuméricos (URL-safe base64-ish).
/// </summary>
public static class ApiKeyGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";  // sem 0/O/I/l/1 para legibilidade
    public const string LivePrefix = "rd_live_";
    public const int SuffixLength = 32;

    /// <summary>Devolve (plainKey, hash, displayPrefix). Plain só pode ser mostrado uma vez.</summary>
    public static (string PlainKey, string Hash, string DisplayPrefix) Generate()
    {
        Span<byte> randomBytes = stackalloc byte[SuffixLength];
        RandomNumberGenerator.Fill(randomBytes);

        var sb = new StringBuilder(SuffixLength);
        for (var i = 0; i < SuffixLength; i++)
            sb.Append(Alphabet[randomBytes[i] % Alphabet.Length]);
        var suffix = sb.ToString();

        var plainKey = LivePrefix + suffix;
        var hash = Hash(plainKey);
        // Prefixo display: "rd_live_" + primeiros 6 do suffix (suficiente para identificar a chave na UI)
        var displayPrefix = LivePrefix + suffix[..6] + "…";
        return (plainKey, hash, displayPrefix);
    }

    /// <summary>SHA256 hex (lowercase). 64 chars de output.</summary>
    public static string Hash(string plainKey)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(plainKey), hashBytes);
        var sb = new StringBuilder(64);
        foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static bool LooksLikeApiKey(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(LivePrefix, StringComparison.Ordinal);
}
