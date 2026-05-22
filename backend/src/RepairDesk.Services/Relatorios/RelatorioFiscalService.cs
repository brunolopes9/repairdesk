using Microsoft.Extensions.Logging;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
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
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IMoloniClient _moloni;
    private readonly ILogger<RelatorioFiscalService> _logger;

    public RelatorioFiscalService(
        IRelatorioFiscalRepository repo,
        ITenantRepository tenants,
        ITenantContext tenant,
        ITenantBillingSettingsRepository billingSettings,
        IMoloniClient moloni,
        ILogger<RelatorioFiscalService> logger)
    {
        _repo = repo;
        _tenants = tenants;
        _tenant = tenant;
        _billingSettings = billingSettings;
        _moloni = moloni;
        _logger = logger;
    }

    public async Task<RelatorioIvaResponse> GetIvaAsync(int ano, int trimestre, int ivaComprasCents = 0, CancellationToken ct = default)
    {
        var tenant = await RequireTenantAsync(ct);
        var (from, to) = Periodo(ano, trimestre);
        var (prevFrom, prevTo) = PeriodoAnterior(ano, trimestre);

        var rawRows = await _repo.ListDocumentosAsync(from, to, ct);
        // Sprint 53: sincroniza com Moloni — exclui (e limpa local) documentos com status=Anulado.
        // Se Moloni nao responder (sandbox 503, timeout, etc), mantemos estado local sem alterar.
        var syncedRows = await SyncWithMoloniAsync(rawRows, ct);

        var docs = BuildDocumentos(syncedRows, tenant.RegimeFiscal);
        var prevDocs = BuildDocumentos(await _repo.ListDocumentosAsync(prevFrom, prevTo, ct), tenant.RegimeFiscal);
        var ivaCompras = Math.Max(0, ivaComprasCents);
        var ivaLiquidado = docs.Sum(d => d.IvaCents);

        // Sprint 159: auto-calcular IVA dedutível das peças stock + Despesas.
        // Assume taxa 23% para o cálculo IVA = cents × 23 / 123 (extracção do IVA embutido no preço com IVA).
        // Regime IsentoArt53 não deduz nada (Bruno fica sem IVA cobrado nem dedutível).
        var ivaDedutivelPecas = 0;
        var ivaDedutivelDespesas = 0;
        IReadOnlyList<IvaDeducaoLinhaDto> comprasStockDetalhe = Array.Empty<IvaDeducaoLinhaDto>();
        IReadOnlyList<IvaDeducaoLinhaDto> despesasOpExDetalhe = Array.Empty<IvaDeducaoLinhaDto>();
        if (tenant.RegimeFiscal != RegimeFiscal.IsentoArt53)
        {
            var pecasCustoComIva = await _repo.SumPecasCustoComIvaAsync(from, to, ct);
            var despesasComIva = await _repo.SumDespesasComIvaAsync(from, to, ct);
            ivaDedutivelPecas = (int)Math.Round(pecasCustoComIva * 23.0 / 123.0);
            ivaDedutivelDespesas = (int)Math.Round(despesasComIva * 23.0 / 123.0);

            // Sprint 180: drill-down detalhe.
            var compras = await _repo.ListComprasStockAsync(from, to, ct);
            var opex = await _repo.ListDespesasOpExAsync(from, to, ct);
            comprasStockDetalhe = compras.Select(l => new IvaDeducaoLinhaDto(
                l.Data, l.Descricao, l.Fornecedor, l.Origem, l.ValorComIvaCents, l.IvaCents)).ToList();
            despesasOpExDetalhe = opex.Select(l => new IvaDeducaoLinhaDto(
                l.Data, l.Descricao, l.Fornecedor, l.Origem, l.ValorComIvaCents, l.IvaCents)).ToList();
        }
        var ivaDedutivelTotal = ivaCompras + ivaDedutivelPecas + ivaDedutivelDespesas;

        return new RelatorioIvaResponse(
            ano,
            trimestre,
            from,
            to,
            docs.Sum(d => d.BaseCents),
            ivaLiquidado,
            ivaCompras,
            ivaDedutivelPecas,
            ivaDedutivelDespesas,
            ivaDedutivelTotal,
            Math.Max(0, ivaLiquidado - ivaDedutivelTotal),
            prevDocs.Sum(d => d.BaseCents),
            prevDocs.Sum(d => d.IvaCents),
            docs,
            comprasStockDetalhe,
            despesasOpExDetalhe);
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

    /// <summary>Verifica cada doc contra Moloni. Os anulados (status=2) sao excluidos do relatorio
    /// E os seus campos Invoice* sao limpos em DB para nao aparecerem em futuras consultas.</summary>
    private async Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> SyncWithMoloniAsync(
        IReadOnlyList<RelatorioFiscalDocumentoRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        if (_tenant.TenantId is not { } tenantId) return rows;

        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        if (settings is null || settings.Provider != BillingProvider.Moloni || string.IsNullOrEmpty(settings.ApiKeyCipherText))
            return rows; // Moloni nao configurado/ligado — nao ha como sincronizar

        var keep = new List<RelatorioFiscalDocumentoRow>(rows.Count);
        foreach (var r in rows)
        {
            if (string.IsNullOrEmpty(r.InvoiceExternalId) || !int.TryParse(r.InvoiceExternalId, out var moloniDocId))
            {
                keep.Add(r);
                continue;
            }

            var status = await _moloni.GetDocumentStatusAsync(settings, moloniDocId, ct);
            // status: null=falha ao verificar (mantem), 0=Rascunho, 1=Fechado, 2=Anulado
            if (status == 2)
            {
                _logger.LogInformation(
                    "Doc Moloni {DocId} ({Tipo} #{Num}) anulado externamente — limpa local.",
                    moloniDocId, r.Tipo, r.NumeroInterno);
                try
                {
                    await _repo.ClearInvoiceFieldsAsync(r.Tipo, r.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha a limpar local apos sync Moloni para {Tipo} {Id}", r.Tipo, r.Id);
                }
                // Nao adiciona ao keep — o documento esta anulado
            }
            else
            {
                keep.Add(r);
            }
        }
        return keep;
    }

    private async Task<Core.Entities.Tenant> RequireTenantAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        return await _tenants.FindByIdAsync(tenantId, ct) ?? throw new NotFoundException("Tenant", tenantId);
    }

    private static IReadOnlyList<RelatorioIvaDocumentoDto> BuildDocumentos(IReadOnlyList<RelatorioFiscalDocumentoRow> rows, RegimeFiscal regime)
    {
        // Sprint 159b: ValorCents da entidade é o TOTAL COM IVA (o que o cliente pagou).
        // ANTES estava a tratar como base e somava IVA por cima (= 80 → IVA 18,40 → erro).
        // Para extrair IVA embutido: base = total × 100 / 123 (com IVA 23%); IVA = total − base.
        // Tenant IsentoArt53 → tudo isento, ValorCents é a base e IVA=0.
        var isExempt = regime == RegimeFiscal.IsentoArt53;
        return rows.Select(r =>
        {
            var totalCents = Math.Max(0, r.ValorCents);
            int baseCents, ivaCents;
            if (isExempt)
            {
                baseCents = totalCents;
                ivaCents = 0;
            }
            else
            {
                // 23% IVA embutido: base = total × 100 / 123.
                baseCents = (int)Math.Round(totalCents * 100.0 / 123.0, MidpointRounding.AwayFromZero);
                ivaCents = totalCents - baseCents;
            }
            return new RelatorioIvaDocumentoDto(
                r.Id,
                r.Tipo,
                r.NumeroInterno,
                string.IsNullOrWhiteSpace(r.InvoiceNumber) ? $"{r.Tipo} #{r.NumeroInterno}" : r.InvoiceNumber!,
                r.InvoiceEmittedAt,
                string.IsNullOrWhiteSpace(r.ClienteNome) ? "Consumidor final" : r.ClienteNome!,
                baseCents,
                ivaCents,
                totalCents);
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
