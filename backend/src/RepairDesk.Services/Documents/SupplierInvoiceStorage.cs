using System.IO.Compression;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 147: storage organizado dos PDFs de fornecedor por tenant/ano/mês/fornecedor.
/// Sprint 169: refactor para usar IPhotoStorage abstracção (Sprint 14). Funciona em:
///   - LocalFileSystem (volume Docker /var/lib/repairdesk/supplier-invoices)
///   - Cloudflare R2 (S3-compat, default em produção quando Storage__Provider=r2)
///
/// Layout: <c>supplier-invoices/{tenantId}/{yyyy}/{MM}/{supplier-slug}/{filename}</c>.
/// Prefix <c>supplier-invoices/</c> isola estes binários dos photos do RepairDesk.
///
/// Multi-tenant safety: path tem que começar por tenantId — paths que escapam disto
/// são rejeitados em CreateZipAsync para impedir exfiltração cross-tenant.
/// </summary>
public interface ISupplierInvoiceStorage
{
    Task<string> SaveAsync(
        Guid tenantId, string supplierSlug, DateTime referenceDate,
        string filename, byte[] pdfBytes, CancellationToken ct = default);

    Task<byte[]> ReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Cria um ZIP em memória com todos os PDFs aprovados no período. Útil para contabilista.
    /// Estrutura interna do zip espelha a do storage ({yyyy}/{MM}/{supplier}/{filename}).
    /// </summary>
    Task<byte[]> CreateZipAsync(
        Guid tenantId, IEnumerable<string> relativePaths, CancellationToken ct = default);
}

public sealed class SupplierInvoiceStorage : ISupplierInvoiceStorage
{
    private const string KeyPrefix = "supplier-invoices/";
    private readonly IPhotoStorage _storage;
    private readonly ILogger<SupplierInvoiceStorage> _logger;

    public SupplierInvoiceStorage(IPhotoStorage storage, ILogger<SupplierInvoiceStorage> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Guid tenantId, string supplierSlug, DateTime referenceDate,
        string filename, byte[] pdfBytes, CancellationToken ct = default)
    {
        var safeSupplier = Slugify(supplierSlug);
        var year = referenceDate.Year.ToString("D4");
        var month = referenceDate.Month.ToString("D2");
        var safeFilename = SanitizeFilename(filename);

        // relativePath: {tenantId}/{yyyy}/{MM}/{supplier}/{filename} — guardado na BD como referência.
        var relativePath = string.Join('/', tenantId.ToString(), year, month, safeSupplier, safeFilename);
        var contentType = ResolveContentType(safeFilename);

        // Storage key inclui prefix para isolar dos photos.
        using var ms = new MemoryStream(pdfBytes);
        await _storage.UploadAsync(KeyPrefix + relativePath, ms, contentType, ct);
        _logger.LogInformation("SupplierInvoice saved at {Path} ({Bytes} bytes, {Type})",
            relativePath, pdfBytes.Length, contentType);
        return relativePath;
    }

    public async Task<byte[]> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            using var stream = await _storage.DownloadAsync(KeyPrefix + relativePath, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotFoundException("SupplierInvoicePdf", relativePath);
        }
    }

    public async Task<byte[]> CreateZipAsync(
        Guid tenantId, IEnumerable<string> relativePaths, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var rel in relativePaths)
            {
                // Segurança: rejeitar paths que não começam pelo tenantId — evita exfiltração cross-tenant.
                if (!rel.StartsWith(tenantId.ToString() + "/", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Skipping path outside tenant scope: {Path}", rel);
                    continue;
                }
                try
                {
                    using var fileStream = await _storage.DownloadAsync(KeyPrefix + rel, ct);
                    // Entry name dentro do zip — sem o tenantId prefix para layout limpo do contabilista.
                    var entryName = rel.Substring(tenantId.ToString().Length + 1);
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    await fileStream.CopyToAsync(entryStream, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PDF missing/unreadable: {Path}", rel);
                }
            }
        }
        return ms.ToArray();
    }

    private static string ResolveContentType(string filename)
    {
        var lower = filename.ToLowerInvariant();
        return lower switch
        {
            _ when lower.EndsWith(".pdf") => "application/pdf",
            _ when lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") => "image/jpeg",
            _ when lower.EndsWith(".png") => "image/png",
            _ when lower.EndsWith(".webp") => "image/webp",
            _ when lower.EndsWith(".gif") => "image/gif",
            _ => "application/octet-stream",
        };
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "desconhecido";
        var lower = input.ToLowerInvariant().Trim();
        var safe = new System.Text.StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) safe.Append(c);
            else if (c is ' ' or '-' or '_') safe.Append('-');
        }
        var result = safe.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "desconhecido" : result;
    }

    private static string SanitizeFilename(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(filename.Where(c => !invalid.Contains(c)));
        return string.IsNullOrWhiteSpace(clean) ? "documento.pdf" : clean;
    }
}
