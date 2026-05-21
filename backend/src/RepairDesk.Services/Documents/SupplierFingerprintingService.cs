using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 162: detecta o fornecedor antes de parsear. Lookup determinístico baseado em:
/// 1. emailMeta.From (regex match contra known patterns + Fornecedor.MatchPatternsJson custom)
/// 2. emailMeta.Subject
/// 3. PDF first 500 chars (texto extraído)
/// 4. Filename hints (raro)
///
/// Reduz dependência do LLM — só fornecedores desconhecidos chegam ao parser LLM (Sprint 163).
/// Tenant pode adicionar fornecedores próprios em Fornecedor.MatchPatternsJson (UI Sprint futuro).
/// </summary>
public interface ISupplierFingerprintingService
{
    Task<SupplierFingerprintResult> DetectAsync(
        SupplierInvoiceEmailMeta? emailMeta,
        string? pdfText,
        string? filename,
        CancellationToken ct = default);
}

public sealed record SupplierFingerprintResult(
    /// <summary>Slug canónico (ex: "tudo4mobile", "utopya"). NULL se não detectado.</summary>
    string? Code,
    /// <summary>Nome amigável para display (ex: "Tudo4Mobile"). NULL se não detectado.</summary>
    string? Name,
    /// <summary>Fornecedor entity match (se existe na BD do tenant). NULL se desconhecido — Bruno cria manualmente ao aprovar.</summary>
    Guid? FornecedorId,
    /// <summary>"known" (hardcoded), "tenant_custom" (MatchPatternsJson), "none" (não detectado).</summary>
    string Source);

public sealed class SupplierFingerprintingService : ISupplierFingerprintingService
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly ILogger<SupplierFingerprintingService> _logger;

    public SupplierFingerprintingService(IFornecedorRepository fornecedores, ILogger<SupplierFingerprintingService> logger)
    {
        _fornecedores = fornecedores;
        _logger = logger;
    }

    /// <summary>
    /// Lista hardcoded de fornecedores conhecidos no espaço PT/EU de reparações móveis.
    /// Cada entry tem: code, name, e patterns regex que devem dar match (qualquer um basta).
    /// Cobre os principais — tenants podem adicionar próprios via Fornecedor.MatchPatternsJson.
    /// </summary>
    private static readonly IReadOnlyList<KnownSupplier> KnownSuppliers = new[]
    {
        new KnownSupplier("tudo4mobile", "Tudo4Mobile", new[]
        {
            @"tudo4mobile", @"geral@tudo4mobile", @"@tudo4mobile\.pt",
        }),
        new KnownSupplier("utopya", "Utopya", new[]
        {
            @"utopya", @"@utopya\.(com|fr)", @"noreply@utopya",
        }),
        new KnownSupplier("molano", "Molano", new[]
        {
            @"molano", @"@molano\.(com|eu)", @"mln-",
        }),
        new KnownSupplier("lcphones", "LC Phones", new[]
        {
            @"lcphones", @"@lcphones",
        }),
        new KnownSupplier("gsmserver", "GSMServer", new[]
        {
            @"gsmserver", @"@gsmserver", @"gsm-server",
        }),
        new KnownSupplier("witrigs", "Witrigs", new[]
        {
            @"witrigs", @"@witrigs",
        }),
        new KnownSupplier("mrphones", "Mr.Phones", new[]
        {
            @"mrphones", @"mr\.?phones", @"@mrphones",
        }),
        new KnownSupplier("ali", "AliExpress/Alibaba", new[]
        {
            @"@aliexpress\.com", @"@alibaba\.com", @"transaction@notice\.alibaba",
        }),
    };

    public async Task<SupplierFingerprintResult> DetectAsync(
        SupplierInvoiceEmailMeta? emailMeta,
        string? pdfText,
        string? filename,
        CancellationToken ct = default)
    {
        // Concatena haystack — minúsculas para case-insensitive matching dentro dos regex.
        var pdfFirstChars = pdfText is { Length: > 500 } ? pdfText[..500] : pdfText ?? "";
        var haystack = string.Join(" \n ", new[]
        {
            emailMeta?.From ?? "",
            emailMeta?.Subject ?? "",
            filename ?? "",
            pdfFirstChars,
        }).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(haystack)) return new(null, null, null, "none");

        // 1. Tenant-custom patterns primeiro (mais específicos, escolha do utilizador).
        var tenantSuppliers = await _fornecedores.ListByTenantAsync(includeInactive: false, ct);
        foreach (var f in tenantSuppliers.Where(x => !string.IsNullOrWhiteSpace(x.MatchPatternsJson)))
        {
            var patterns = ParsePatterns(f.MatchPatternsJson!);
            if (patterns.Any(p => SafeRegexMatch(haystack, p)))
            {
                _logger.LogInformation("Supplier detected (tenant_custom): {Code} {Name}", f.Code, f.Name);
                return new(f.Code, f.Name, f.Id, "tenant_custom");
            }
        }

        // 2. Known patterns hardcoded.
        foreach (var known in KnownSuppliers)
        {
            if (known.Patterns.Any(p => SafeRegexMatch(haystack, p)))
            {
                // Tenta linkar à Fornecedor entity do tenant (se já existe por code).
                var fornecedor = tenantSuppliers.FirstOrDefault(x =>
                    string.Equals(x.Code, known.Code, StringComparison.OrdinalIgnoreCase));
                _logger.LogInformation("Supplier detected (known): {Code} {Name} fornecedorId={FId}",
                    known.Code, known.Name, fornecedor?.Id);
                return new(known.Code, known.Name, fornecedor?.Id, "known");
            }
        }

        return new(null, null, null, "none");
    }

    private static IEnumerable<string> ParsePatterns(string json)
    {
        try
        {
            var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
            return arr ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Regex.IsMatch com timeout 100ms para evitar ReDoS de patterns mal-escritos.</summary>
    private static bool SafeRegexMatch(string haystack, string pattern)
    {
        try
        {
            return Regex.IsMatch(haystack, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }

    private sealed record KnownSupplier(string Code, string Name, IReadOnlyList<string> Patterns);
}
