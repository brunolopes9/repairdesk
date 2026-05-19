using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Relatorios;

public interface IRelatorioFiscalService
{
    Task<RelatorioIvaResponse> GetIvaAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default);
    Task<byte[]> ExportIvaCsvAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default);
    Task<(byte[] Pdf, string Filename)> ExportIvaPdfAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default);
}

public sealed class RelatorioFiscalService : IRelatorioFiscalService
{
    private readonly IRelatorioFiscalRepository _repo;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;

    public RelatorioFiscalService(IRelatorioFiscalRepository repo, ITenantRepository tenants, ITenantContext tenant)
    {
        _repo = repo;
        _tenants = tenants;
        _tenant = tenant;
    }

    public async Task<RelatorioIvaResponse> GetIvaAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var tenant = await RequireTenantAsync(ct);
        var (from, to) = Periodo(ano, trimestre);
        var (prevFrom, prevTo) = PeriodoAnterior(ano, trimestre);

        var docs = BuildDocumentos(await _repo.ListDocumentosAsync(from, to, ct), tenant.RegimeFiscal);
        var prevDocs = BuildDocumentos(await _repo.ListDocumentosAsync(prevFrom, prevTo, ct), tenant.RegimeFiscal);
        var ivaCompras = Math.Max(0, ivaComprasCents);
        var ivaLiquidado = docs.Sum(d => d.IvaCents);

        return new RelatorioIvaResponse(
            ano,
            trimestre,
            from,
            to,
            docs.Sum(d => d.BaseCents),
            ivaLiquidado,
            ivaCompras,
            Math.Max(0, ivaLiquidado - ivaCompras),
            prevDocs.Sum(d => d.BaseCents),
            prevDocs.Sum(d => d.IvaCents),
            docs);
    }

    public async Task<byte[]> ExportIvaCsvAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var report = await GetIvaAsync(ano, trimestre, ivaComprasCents, ct);
        var csv = new CsvBuilder();
        csv.Row("data", "tipo", "numero", "cliente", "base", "iva", "total");
        foreach (var d in report.Documentos)
            csv.Row(d.Data, d.Tipo, d.NumeroDocumento, d.Cliente, ToEuros(d.BaseCents), ToEuros(d.IvaCents), ToEuros(d.TotalCents));
        return csv.ToUtf8WithBom();
    }

    public async Task<(byte[] Pdf, string Filename)> ExportIvaPdfAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var tenant = await RequireTenantAsync(ct);
        var report = await GetIvaAsync(ano, trimestre, ivaComprasCents, ct);
        var pdf = RelatorioIvaPdfRenderer.Render(tenant.Name, tenant.Nif, report);
        return (pdf, $"relatorio_iva_{ano}_T{trimestre}.pdf");
    }

    private async Task<Core.Entities.Tenant> RequireTenantAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        return await _tenants.FindByIdAsync(tenantId, ct) ?? throw new NotFoundException("Tenant", tenantId);
    }

    private static IReadOnlyList<RelatorioIvaDocumentoDto> BuildDocumentos(IReadOnlyList<RelatorioFiscalDocumentoRow> rows, RegimeFiscal regime)
    {
        var vatRate = regime == RegimeFiscal.IsentoArt53 ? 0m : 0.23m;
        return rows.Select(r =>
        {
            var baseCents = Math.Max(0, r.ValorCents);
            var ivaCents = (int)Math.Round(baseCents * vatRate, MidpointRounding.AwayFromZero);
            return new RelatorioIvaDocumentoDto(
                r.Id,
                r.Tipo,
                r.NumeroInterno,
                string.IsNullOrWhiteSpace(r.InvoiceNumber) ? $"{r.Tipo} #{r.NumeroInterno}" : r.InvoiceNumber!,
                r.InvoiceEmittedAt,
                string.IsNullOrWhiteSpace(r.ClienteNome) ? "Consumidor final" : r.ClienteNome!,
                baseCents,
                ivaCents,
                baseCents + ivaCents);
        }).ToList();
    }

    private static (DateTime FromUtc, DateTime ToUtc) Periodo(int ano, int trimestre)
    {
        if (ano is < 2000 or > 2100) throw new ValidationException("invalid_year", "Ano invalido.");
        if (trimestre is < 1 or > 4) throw new ValidationException("invalid_quarter", "Trimestre invalido.");
        var from = new DateTime(ano, (trimestre - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(3));
    }

    private static (DateTime FromUtc, DateTime ToUtc) PeriodoAnterior(int ano, int trimestre)
    {
        var (from, _) = Periodo(ano, trimestre);
        return (from.AddMonths(-3), from);
    }

    private static decimal ToEuros(int cents) => cents / 100m;
}
