using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Files;

/// <summary>
/// Implementação de <see cref="IFileValidator"/> baseada em magic bytes.
///
/// Assinaturas suportadas (Doc 73 §4):
/// - JPEG: <c>FF D8 FF</c>
/// - PNG:  <c>89 50 4E 47 0D 0A 1A 0A</c>
/// - WebP: <c>RIFF....WEBP</c> (bytes 0-3 = "RIFF", bytes 8-11 = "WEBP")
/// - GIF:  <c>GIF87a</c> ou <c>GIF89a</c>
/// - HEIC: bytes 4-11 começam com <c>ftypheic</c>, <c>ftypheix</c>, <c>ftyphevc</c> ou <c>ftypmif1</c>
/// - PDF:  <c>%PDF-</c> (25 50 44 46 2D)
///
/// Whitelist por FileKind:
/// - <see cref="FileKind.Image"/>: JPEG, PNG, WebP, GIF, HEIC
/// - <see cref="FileKind.Pdf"/>:   PDF
/// </summary>
public sealed class FileValidator : IFileValidator
{
    /// <summary>Quantos bytes ler do início para identificar o formato.</summary>
    private const int HeaderProbeBytes = 16;

    public async Task<ValidatedFile> ValidateAsync(Stream content, string declaredMime, FileKind kind, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Copia tudo para memory — necessário porque os streams de IFormFile não suportam
        // seek consistente e os consumers (storage, LLM) precisam de re-ler. Limite de
        // tamanho deve já ter sido aplicado via [RequestSizeLimit] no controller.
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var buffer = ms.ToArray();

        if (buffer.Length == 0)
            throw new ValidationException("file_empty", "Ficheiro vazio.");
        if (buffer.Length < 8)
            throw new ValidationException("file_invalid", "Ficheiro demasiado curto para ser válido.");

        var header = buffer.AsSpan(0, Math.Min(HeaderProbeBytes, buffer.Length));
        var detected = Detect(header);

        if (detected is null)
            throw new ValidationException("file_unknown_type",
                $"Não foi possível identificar o tipo de ficheiro pelo conteúdo (declarado: {declaredMime}).");

        var allowed = kind switch
        {
            FileKind.Image => detected.Mime is "image/jpeg" or "image/png" or "image/webp" or "image/gif" or "image/heic",
            FileKind.Pdf => detected.Mime is "application/pdf",
            _ => false,
        };
        if (!allowed)
            throw new ValidationException("file_type_not_allowed",
                $"Tipo {detected.Mime} não é aceite neste endpoint (esperado: {kind}).");

        return new ValidatedFile(detected.Mime, detected.Extension, buffer);
    }

    private static Signature? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return new Signature("image/jpeg", ".jpg");

        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
            && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            return new Signature("image/png", ".png");

        if (h.Length >= 12
            && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46  // RIFF
            && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50)  // WEBP
            return new Signature("image/webp", ".webp");

        if (h.Length >= 6 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38
            && (h[4] == 0x37 || h[4] == 0x39) && h[5] == 0x61)
            return new Signature("image/gif", ".gif");

        // HEIC/HEIF: bytes 4-7 = "ftyp", bytes 8-11 = brand. Aceitamos brands comuns.
        if (h.Length >= 12 && h[4] == 0x66 && h[5] == 0x74 && h[6] == 0x79 && h[7] == 0x70)
        {
            var brand = System.Text.Encoding.ASCII.GetString(h.Slice(8, 4));
            if (brand is "heic" or "heix" or "hevc" or "mif1" or "msf1")
                return new Signature("image/heic", ".heic");
        }

        if (h.Length >= 5 && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46 && h[4] == 0x2D)
            return new Signature("application/pdf", ".pdf");

        return null;
    }

    private sealed record Signature(string Mime, string Extension);
}
