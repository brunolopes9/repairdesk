using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;

namespace RepairDesk.Services.Documentos;

/// <summary>
/// Sprint 513/514: lista única de documentos de VENDA (Fatura / Fatura Simplificada / NC / …).
/// Funde o que o Mender emitiu (com link à reparação/venda) com o que existe no Moloni (histórico +
/// notas de crédito + documentos feitos directamente no painel), usando os valores e estado REAIS
/// do Moloni. É o backbone do separador "Vendas" de Compras e Operação — o Mender como sítio único
/// onde se vêem todas as faturas. Degrada com elegância: se o Moloni falhar, mostra só o local.
/// </summary>
public interface IDocumentoService
{
    Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default);
    Task<byte[]> ExportVendasCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);

    /// <summary>Sprint 527: emite um Recibo Moloni que liquida uma Fatura a crédito (em dívida).</summary>
    Task<ReciboResultDto> EmitirReciboAsync(int documentId, CancellationToken ct = default);
}

/// <summary>Sprint 527: resultado da emissão de um Recibo de liquidação.</summary>
public sealed record ReciboResultDto(int ReceiptId, string? Numero, int ValorCents);

public sealed record DocumentoDto(
    Guid Id,
    string Origem,            // "Venda" | "Reparacao" | "Trabalho" | "Moloni"
    int NumeroInterno,
    string Tipo,             // "Fatura" | "Fatura Simplificada" | "Nota de Crédito" | …
    string TipoCodigo,       // "FT" | "FS" | "NC" | …
    string? Numero,          // ex: "FT 2026/2"
    string? ExternalId,
    string? PdfUrl,
    string Provider,         // "Moloni" | "InvoiceXpress" | "None"
    DateTime Data,
    Guid? ClienteId,
    string? ClienteNome,
    string? ClienteNif,
    int TotalCents,          // com IVA
    int IvaCents,
    int BaseCents,
    string Estado,           // "Ativo" | "Anulado" | "Rascunho" | "—"
    // Sprint 528: se preenchido, a fatura está liquidada por este recibo → esconde "Emitir recibo".
    string? ReciboNumero = null,
    DateTime? ReciboEmitidoEm = null);

public sealed record DocumentosListDto(
    IReadOnlyList<DocumentoDto> Items,
    int TotalDocumentos,
    int TotalCents,
    int TotalIvaCents,
    int TotalBaseCents);

public sealed record DocumentosFiltro(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Q,
    string? Tipo);

/// <summary>Mapeia o prefixo SAF-T do número Moloni (ex: "FT 2026/2") para tipo legível.</summary>
public static class DocumentoTipo
{
    public static (string Codigo, string Nome) FromNumero(string? numero)
    {
        var prefixo = (numero ?? string.Empty).TrimStart().Split(' ', '/', '-', '.')[0].ToUpperInvariant();
        return prefixo switch
        {
            "FT" => ("FT", "Fatura"),
            "FS" => ("FS", "Fatura Simplificada"),
            "FR" => ("FR", "Fatura-Recibo"),
            "NC" => ("NC", "Nota de Crédito"),
            "ND" => ("ND", "Nota de Débito"),
            "FC" => ("FC", "Fatura de Consignação"),
            "VD" => ("VD", "Venda a Dinheiro"),
            "RG" or "REC" => ("RG", "Recibo"),
            "ORC" or "PF" => ("ORC", "Orçamento"),
            _ => ("FT", "Fatura"),
        };
    }
}

public sealed class DocumentoService : IDocumentoService
{
    // Documentos de venda que entram nesta lista (exclui guias de transporte, orçamentos, etc).
    private static readonly HashSet<string> VendaSaftCodes = new(StringComparer.OrdinalIgnoreCase)
        { "FT", "FS", "FR", "NC", "ND", "VD" };

    private readonly IRelatorioFiscalRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IMoloniClient _moloni;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentoService> _logger;

    public DocumentoService(
        IRelatorioFiscalRepository repo,
        ITenantContext tenant,
        ITenantBillingSettingsRepository billingSettings,
        IMoloniClient moloni,
        IMemoryCache cache,
        ILogger<DocumentoService> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _billingSettings = billingSettings;
        _moloni = moloni;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(filtro.FromUtc, filtro.ToUtc);

        // 1. Documentos emitidos pelo Mender (mantêm link à origem reparação/venda/trabalho).
        var localRows = await _repo.ListVendaDocumentosDetalheAsync(from, to, ct);
        var byExternalId = new Dictionary<string, DocumentoDto>(StringComparer.OrdinalIgnoreCase);
        var semExternalId = new List<DocumentoDto>();
        foreach (var r in localRows)
        {
            var dto = MapLocal(r);
            if (!string.IsNullOrWhiteSpace(r.InvoiceExternalId))
                byExternalId[r.InvoiceExternalId!] = dto;
            else
                semExternalId.Add(dto);
        }

        // 2. Fetch ao Moloni (cache 5min, graceful): histórico + NCs + documentos feitos no painel.
        //    Para os Mender-emitidos, sobrepõe os valores + estado REAIS do Moloni à estimativa local.
        foreach (var m in await FetchMoloniDocsAsync(ct))
        {
            if (m.Data < from || m.Data >= to) continue;
            if (m.SaftCode is not null && !VendaSaftCodes.Contains(m.SaftCode)) continue;

            var key = m.DocumentId.ToString(CultureInfo.InvariantCulture);
            if (byExternalId.TryGetValue(key, out var local))
            {
                byExternalId[key] = local with
                {
                    TotalCents = m.GrossCents,
                    IvaCents = m.TaxesCents,
                    BaseCents = m.NetCents,
                    Estado = EstadoLabel(m.Status),
                };
            }
            else
            {
                byExternalId[key] = MapMoloni(m);
            }
        }

        IEnumerable<DocumentoDto> items = byExternalId.Values.Concat(semExternalId);

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
            items = items.Where(d => string.Equals(d.TipoCodigo, filtro.Tipo, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var q = filtro.Q.Trim();
            items = items.Where(d =>
                (d.Numero?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.ClienteNome?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.ClienteNif?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = items.OrderByDescending(d => d.Data).ToList();

        // Sprint 521: os totais ("faturado") contam SÓ documentos de venda ATIVOS — exclui Anulados,
        // Rascunhos e Notas de Crédito. Estes continuam a aparecer na lista (com badge), mas não inflam
        // o total. Sem este filtro, anulados + rascunhos (0,00€) eram somados e davam totais absurdos
        // (ex: base sem IVA > total com IVA, que é matematicamente impossível).
        var faturado = list
            .Where(d => d.Estado == "Ativo" && d.TipoCodigo is "FT" or "FS" or "FR" or "VD")
            .ToList();
        return new DocumentosListDto(
            list,
            list.Count,
            faturado.Sum(d => d.TotalCents),
            faturado.Sum(d => d.IvaCents),
            faturado.Sum(d => d.BaseCents));
    }

    public async Task<byte[]> ExportVendasCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var list = await ListVendasAsync(new DocumentosFiltro(fromUtc, toUtc, null, null), ct);
        var csv = new CsvBuilder();
        csv.Row("data", "tipo", "numero", "cliente", "nif", "origem", "base_eur", "iva_eur", "total_eur", "estado");
        foreach (var d in list.Items)
        {
            csv.Row(
                d.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                d.Tipo,
                d.Numero ?? string.Empty,
                d.ClienteNome ?? string.Empty,
                d.ClienteNif ?? string.Empty,
                d.Origem,
                (d.BaseCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                (d.IvaCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                (d.TotalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                d.Estado);
        }
        return csv.ToUtf8WithBom();
    }

    public async Task<ReciboResultDto> EmitirReciboAsync(int documentId, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        if (settings?.Provider != BillingProvider.Moloni || settings.CompanyId is null or <= 0)
            throw new ValidationException("moloni_not_configured", "O Moloni não está configurado para este tenant.");

        // Resolve o documento na FONTE REAL (Moloni), sem cache — nunca confiar em valores do cliente.
        var docs = await _moloni.ListDocumentsAsync(settings, ct);
        var row = docs.FirstOrDefault(d => d.DocumentId == documentId)
            ?? throw new NotFoundException("Documento Moloni", documentId);

        // Só a Fatura pura (FT) é "a crédito"/em dívida. FS, FR e VD já são pagamento imediato.
        if (!string.Equals(row.SaftCode, "FT", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("recibo_so_fatura",
                "Só faturas a crédito (FT) podem receber recibo — FS / Fatura-Recibo / VD já estão pagas.");
        if (row.Status == 2)
            throw new ValidationException("recibo_doc_anulado", "Documento anulado — não pode receber recibo.");

        var result = await _moloni.InsertReceiptAsync(
            settings, row.CustomerId, documentId, row.GrossCents, $"Liquidação da fatura {row.Numero}", ct);

        // Sprint 528: marca o recibo na entity local (Reparacao/Trabalho/Venda) → esconde o botão
        // "Emitir recibo" (evita duplicados) e mostra o recibo na ficha. No-op se for doc só-Moloni.
        await _repo.MarcarReciboEmitidoAsync(
            documentId.ToString(CultureInfo.InvariantCulture),
            result.Numero ?? $"Recibo #{result.ReceiptId}", DateTime.UtcNow, ct);

        _cache.Remove($"moloni-docs:{tenantId}"); // força a lista a refletir o novo estado.
        _logger.LogInformation("Recibo {ReceiptId} emitido p/ fatura {Numero} (doc {DocumentId})",
            result.ReceiptId, row.Numero, documentId);
        return new ReciboResultDto(result.ReceiptId, result.Numero, row.GrossCents);
    }

    private async Task<IReadOnlyList<MoloniDocumentRow>> FetchMoloniDocsAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Array.Empty<MoloniDocumentRow>();

        var cacheKey = $"moloni-docs:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MoloniDocumentRow>? cached) && cached is not null)
            return cached;

        try
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider != BillingProvider.Moloni || settings.CompanyId is null or <= 0)
                return Array.Empty<MoloniDocumentRow>();

            var docs = await _moloni.ListDocumentsAsync(settings, ct);
            _cache.Set(cacheKey, docs, TimeSpan.FromMinutes(5));
            return docs;
        }
        catch (Exception ex)
        {
            // Graceful: Moloni em baixo / token expirado → mostramos só os documentos locais.
            _logger.LogWarning(ex, "Fetch de documentos Moloni falhou; lista mostra apenas o que o Mender já tem.");
            return Array.Empty<MoloniDocumentRow>();
        }
    }

    private static DocumentoDto MapLocal(DocumentoVendaRow r)
    {
        var (codigo, nome) = DocumentoTipo.FromNumero(r.InvoiceNumber);
        // IVA estimado a 23% — substituído pelos valores exactos do Moloni quando há match (fetch).
        var baseCents = (int)Math.Round(r.TotalCents / 1.23m, MidpointRounding.AwayFromZero);
        return new DocumentoDto(
            r.Id, r.Origem, r.NumeroInterno, nome, codigo,
            r.InvoiceNumber, r.InvoiceExternalId, r.InvoicePdfUrl,
            r.Provider.ToString(), r.InvoiceEmittedAt,
            r.ClienteId, r.ClienteNome, r.ClienteNif,
            r.TotalCents, r.TotalCents - baseCents, baseCents, "Ativo",
            r.ReciboNumero, r.ReciboEmitidoEm);
    }

    private static DocumentoDto MapMoloni(MoloniDocumentRow m)
    {
        var (codigo, nome) = DocumentoTipo.FromNumero(m.Numero);
        return new DocumentoDto(
            Guid.Empty, "Moloni", 0, nome, codigo,
            m.Numero, m.DocumentId.ToString(CultureInfo.InvariantCulture), null,
            "Moloni", m.Data, null, m.EntityName, m.EntityVat,
            m.GrossCents, m.TaxesCents, m.NetCents, EstadoLabel(m.Status));
    }

    private static string EstadoLabel(int status) => status switch
    {
        1 => "Ativo",
        2 => "Anulado",
        0 => "Rascunho",
        _ => "—",
    };

    private static (DateTime from, DateTime to) ResolveRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? DateTime.UtcNow.Date.AddDays(1);
        var from = fromUtc ?? new DateTime(to.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, to);
    }
}
