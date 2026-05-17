using System.Security.Cryptography;

namespace RepairDesk.Common.Helpers;

/// <summary>
/// Slug curto, não sequencial, alfanumérico para URLs públicas de reparações.
/// Usa charset sem ambíguos (sem 0/O, 1/I/L). 10 chars → ~57 bits de entropia,
/// suficiente para resistir a brute-force se o endpoint público tiver rate-limit.
/// </summary>
public static class PublicSlugGenerator
{
    private const string Charset = "23456789ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
    private const int Length = 10;

    public static string New()
    {
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[Length];
        for (int i = 0; i < Length; i++)
        {
            chars[i] = Charset[bytes[i] % Charset.Length];
        }
        return new string(chars);
    }
}
