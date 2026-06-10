using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documentos;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Relatorios;

/// <summary>
/// Sprint 542: Extrato unificado para o contabilista — TODOS os movimentos do período por data:
/// Vendas (lista única local+Moloni: FT/FS/FR/NC/ND/VD/RG, incl. documentos feitos só no painel
/// Moloni), Compras de stock (entradas + despesas Peças/Material) e Despesas operacionais.
/// Equivalente ao "Extrato de Vendas/Compras" do Moloni, mas num só PDF — o Mender como ERP único.
/// </summary>
public interface IExtratoService
{
    Task<(byte[] Pdf, string Filename)> ExportPdfAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

/// <summary>Totais do extrato, calculados com regras fiscais explícitas (ver ComputeTotais).</summary>
public sealed record ExtratoTotais(
    int FaturadoCents,    // FT+FS+FR+VD+ND ativos − NC ativas (com IVA)
    int IvaVendasCents,
    int ComprasCents,     // entradas de stock + despesas Peças/Material (com IVA)
    int DespesasCents,    // OpEx (com IVA)
    int ResultadoCents);  // Faturado − Compras − Despesas (tesouraria simples, valores com IVA)

public sealed class ExtratoService : IExtratoService
{
    private readonly IDocumentoService _documentos;
    private readonly IRelatorioFiscalRepository _fiscal;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;

    public ExtratoService(
        IDocumentoService documentos,
        IRelatorioFiscalRepository fiscal,
        ITenantRepository tenants,
        ITenantContext tenant)
    {
        _documentos = documentos;
        _fiscal = fiscal;
        _tenants = tenants;
        _tenant = tenant;
    }

    public async Task<(byte[] Pdf, string Filename)> ExportPdfAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (toUtc <= fromUtc)
            throw new ValidationException("invalid_period", "O fim do período tem de ser depois do início.");
        if ((toUtc - fromUtc).TotalDays > 731)
            throw new ValidationException("period_too_long", "Período máximo do extrato: 2 anos.");

        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        var tenant = await _tenants.FindByIdAsync(tenantId, ct) ?? throw new NotFoundException("Tenant", tenantId);

        var vendas = await _documentos.ListVendasAsync(new DocumentosFiltro(fromUtc, toUtc, null, null), ct);
        var compras = await _fiscal.ListComprasStockAsync(fromUtc, toUtc, ct);
        var despesas = await _fiscal.ListDespesasOpExAsync(fromUtc, toUtc, ct);
        var totais = ComputeTotais(vendas.Items, compras, despesas);

        var pdf = ExtratoPdfRenderer.Render(tenant.Name, tenant.Nif, fromUtc, toUtc, vendas.Items, compras, despesas, totais);
        return (pdf, $"extrato_{fromUtc:yyyyMMdd}_{toUtc.AddDays(-1):yyyyMMdd}.pdf");
    }

    /// <summary>
    /// Regras dos totais (explícitas e independentes da lista única, para o PDF ser determinístico):
    /// Anulados/Rascunhos não somam (mas aparecem listados com o estado). Recibos (RG) são
    /// liquidações — não são faturação. Orçamentos (ORC) são propostas. NC subtraem.
    /// </summary>
    public static ExtratoTotais ComputeTotais(
        IReadOnlyList<DocumentoDto> vendas,
        IReadOnlyList<IvaDeducaoLinha> compras,
        IReadOnlyList<IvaDeducaoLinha> despesas)
    {
        int faturado = 0, iva = 0;
        foreach (var d in vendas)
        {
            if (d.Estado is "Anulado" or "Rascunho") continue;
            if (d.TipoCodigo is "RG" or "ORC") continue;
            var sinal = d.TipoCodigo == "NC" ? -1 : 1;
            faturado += sinal * d.TotalCents;
            iva += sinal * d.IvaCents;
        }

        var comprasCents = compras.Sum(c => c.ValorComIvaCents);
        var despesasCents = despesas.Sum(x => x.ValorComIvaCents);
        return new ExtratoTotais(faturado, iva, comprasCents, despesasCents, faturado - comprasCents - despesasCents);
    }
}
