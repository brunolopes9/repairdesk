using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 147: storage organizado dos PDFs de fornecedor por tenant/ano/mês/fornecedor.
/// Layout: <c>{root}/{tenantId}/{yyyy}/{MM}/{supplier-slug}/{filename}.pdf</c>.
///
/// Root path vem de <c>Storage:SupplierInvoicesRoot</c> ou env <c>STORAGE_SUPPLIER_INVOICES_ROOT</c>.
/// Default: <c>/var/lib/repairdesk/supplier-invoices</c> (visível no container Docker).
///
/// Para sync automático com Drive/Dropbox o tenant monta o volume como bind mount no host
/// (docker-compose: <c>./data/supplier-invoices:/var/lib/repairdesk/supplier-invoices</c> ou
/// <c>~/Dropbox/RepairDesk/faturas:/var/lib/repairdesk/supplier-invoices</c>).
/// </summary>
public interface ISupplierInvoiceStorage
{
    Task<string> SaveAsync(
        Guid tenantId, string supplierSlug, DateTime referenceDate,
        string filename, byte[] pdfBytes, CancellationToken ct = default);

    Task<byte[]> ReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Cria um ZIP em memória com todos os PDFs aprovados no período. UseFul para contabilista.
    /// Estrutura interna do zip espelha a do filesystem ({yyyy}/{MM}/{supplier}/{filename}).
    /// </summary>
    Task<byte[]> CreateZipAsync(
        Guid tenantId, IEnumerable<string> relativePaths, CancellationToken ct = default);
}

public sealed class SupplierInvoiceStorage : ISupplierInvoiceStorage
{
    private readonly string _rootPath;
    private readonly ILogger<SupplierInvoiceStorage> _logger;

    public SupplierInvoiceStorage(IConfiguration config, ILogger<SupplierInvoiceStorage> logger)
    {
        _rootPath = config["Storage:SupplierInvoicesRoot"]
            ?? Environment.GetEnvironmentVariable("STORAGE_SUPPLIER_INVOICES_ROOT")
            ?? "/var/lib/repairdesk/supplier-invoices";
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

        // Path layout: {tenantId}/{yyyy}/{MM}/{supplier}/{filename}.
        var relativePath = Path.Combine(tenantId.ToString(), year, month, safeSupplier, safeFilename)
            .Replace('\\', '/');
        var absolutePath = Path.Combine(_rootPath, relativePath);

        var dir = Path.GetDirectoryName(absolutePath)!;
        Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(absolutePath, pdfBytes, ct);
        _logger.LogInformation("SupplierInvoice saved at {Path} ({Bytes} bytes)", relativePath, pdfBytes.Length);
        return relativePath;
    }

    public async Task<byte[]> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolute(relativePath);
        if (!File.Exists(absolutePath))
            throw new NotFoundException("SupplierInvoicePdf", relativePath);
        return await File.ReadAllBytesAsync(absolutePath, ct);
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
                var absolutePath = ResolveAbsolute(rel);
                if (!File.Exists(absolutePath))
                {
                    _logger.LogWarning("PDF missing on filesystem: {Path}", rel);
                    continue;
                }

                // Entry name dentro do zip — sem o tenantId prefix para o contabilista ver layout limpo.
                var entryName = rel.Substring(tenantId.ToString().Length + 1);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(absolutePath);
                await fileStream.CopyToAsync(entryStream, ct);
            }
        }
        return ms.ToArray();
    }

    private string ResolveAbsolute(string relativePath)
    {
        // Anti path-traversal — relativePath nunca pode escapar do root.
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootFull = Path.GetFullPath(_rootPath);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("invalid_path", $"Path inválido: {relativePath}");
        return combined;
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
