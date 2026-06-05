using System.Globalization;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Services.Documentos;

/// <summary>
/// Sprint 513: lista única de documentos de VENDA emitidos (Fatura / Fatura Simplificada / …),
/// agregando Reparações + Trabalhos + Vendas POS. É o backbone do separador "Vendas" em
/// Compras e Operação — o Mender como sítio único onde se vêem todas as faturas. Leitura pura
/// (sem tabela nova): reusa a query cross-entity do RelatorioFiscalRepository.
/// </summary>
public interface IDocumentoService
{
    Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default);
    Task<byte[]> ExportVendasCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

public sealed record DocumentoDto(
    Guid Id,
    string Origem,            // "Venda" | "Reparacao" | "Trabalho"
    int NumeroInterno,
    string Tipo,             // "Fatura" | "Fatura Simplificada" | "Nota de Crédito" | …
    string TipoCodigo,       // "FT" | "FS" | "NC" | …
    string? Numero,          // ex: "FT M/2"
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
    string Estado);          // "Ativo" (reconciliação de anulações Moloni chega numa fase futura)

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

/// <summary>Mapeia o prefixo SAF-T do número Moloni (ex: "FT M/2") para tipo legível.</summary>
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
    private readonly IRelatorioFiscalRepository _repo;

    public DocumentoService(IRelatorioFiscalRepository repo) => _repo = repo;

    public async Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(filtro.FromUtc, filtro.ToUtc);
        var rows = await _repo.ListVendaDocumentosDetalheAsync(from, to, ct);

        IEnumerable<DocumentoDto> items = rows.Select(Map);

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

        var list = items.ToList();
        return new DocumentosListDto(
            list,
            list.Count,
            list.Sum(d => d.TotalCents),
            list.Sum(d => d.IvaCents),
            list.Sum(d => d.BaseCents));
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

    private static DocumentoDto Map(DocumentoVendaRow r)
    {
        var (codigo, nome) = DocumentoTipo.FromNumero(r.InvoiceNumber);
        // IVA embutido a 23% (LopesTech opera maioritariamente a esta taxa). Base = total / 1.23.
        // Aproximação assumida para o painel; o SAF-T do Moloni continua a ser a fonte legal.
        var baseCents = (int)Math.Round(r.TotalCents / 1.23m, MidpointRounding.AwayFromZero);
        var ivaCents = r.TotalCents - baseCents;
        return new DocumentoDto(
            r.Id, r.Origem, r.NumeroInterno, nome, codigo,
            r.InvoiceNumber, r.InvoiceExternalId, r.InvoicePdfUrl,
            r.Provider.ToString(), r.InvoiceEmittedAt,
            r.ClienteId, r.ClienteNome, r.ClienteNif,
            r.TotalCents, ivaCents, baseCents, "Ativo");
    }

    private static (DateTime from, DateTime to) ResolveRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? DateTime.UtcNow.Date.AddDays(1);
        var from = fromUtc ?? new DateTime(to.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, to);
    }
}
