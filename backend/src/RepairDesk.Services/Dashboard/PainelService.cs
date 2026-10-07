using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fiscal;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.Dashboard;

/// <summary>Resumo do mês corrente (vendas entregues + despesas). Valores em euros.</summary>
public sealed record PainelMesDto(
    DateTime De,
    DateTime Ate,
    int Vendas,
    decimal Faturado,
    decimal IvaNasVendas,
    decimal IvaAEntregar,
    decimal LucroVendas,
    decimal Despesas);

public sealed record PainelEmCursoDto(int Orcamentos, int EmCurso, int AEsperaPeca, int Prontas);

public sealed record PainelAlertasDto(
    int FaturasPorRegistar,
    int ComprasSemFatura,
    int ComprasComDiferenca,
    int FaturasRecebidasPorAprovar);

public sealed record PainelStockDto(int Unidades, decimal ValorPago, decimal LucroSeVenderTudo);

public sealed record PainelVendaResumoDto(
    Guid Id,
    int Numero,
    VendaTipo Tipo,
    VendaEstado Estado,
    string? Cliente,
    string? Descricao,
    DateTime? PrevistoPara,
    int TotalCents);

public sealed record PainelDto(
    PainelMesDto Mes,
    PainelEmCursoDto EmCurso,
    PainelAlertasDto Alertas,
    PainelStockDto Stock,
    IReadOnlyList<PainelVendaResumoDto> ProximasEntregas);

public interface IPainelService
{
    Task<PainelDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Doc 94: Dashboard do modelo novo — só Vendas, Compras (stock por lote) e Despesas. Todas as contas de
/// IVA vêm do motor (<see cref="IvaEngine"/>). O IVA trimestral completo (com despesas e autoliquidação)
/// vive em "IVA &amp; Resultados" (Fase 5).
/// </summary>
public sealed class PainelService : IPainelService
{
    private const int MaxVendasMes = 5000;

    private readonly IVendaRepository _vendas;
    private readonly ICompraService _compras;
    private readonly IDespesaRepository _despesas;
    private readonly ISupplierInvoiceImportRepository _imports;
    private readonly ITenantContext _tenant;

    public PainelService(
        IVendaRepository vendas,
        ICompraService compras,
        IDespesaRepository despesas,
        ISupplierInvoiceImportRepository imports,
        ITenantContext tenant)
    {
        _vendas = vendas;
        _compras = compras;
        _despesas = despesas;
        _imports = imports;
        _tenant = tenant;
    }

    public async Task<PainelDto> GetAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var de = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = de.AddMonths(1);

        var (entregues, _) = await _vendas.SearchAsync(new VendaFiltro(de, ate, Estado: VendaEstado.Entregue), 1, MaxVendasMes, ct);
        var contas = entregues.SelectMany(v => v.Items).Select(VendaService.Calcular).ToList();

        var (despesas, _) = await _despesas.SearchAsync(null, null, null, false, false, de, ate.AddTicks(-1), null, 1, 10_000, ct);

        var (emCurso, _) = await _vendas.SearchAsync(new VendaFiltro(EmCurso: true), 1, 1000, ct);
        var (porFaturar, totalPorFaturar) = await _vendas.SearchAsync(new VendaFiltro(FaturaPorRegistar: true), 1, 1, ct);

        var resumoCompras = await _compras.ResumoAsync(ct);
        var pendentes = _tenant.TenantId is { } tenantId ? (await _imports.ListPendingAsync(tenantId, 500, ct)).Count : 0;

        var proximas = emCurso
            .OrderBy(v => v.Estado == VendaEstado.Pronta ? 0 : 1)
            .ThenBy(v => v.PrevistoPara ?? DateTime.MaxValue)
            .ThenBy(v => v.CreatedAt)
            .Take(8)
            .Select(v => new PainelVendaResumoDto(
                v.Id, v.Numero, v.Tipo, v.Estado, v.Cliente?.Nome,
                v.Equipamento ?? v.Problema ?? v.Items.FirstOrDefault()?.Descricao,
                v.PrevistoPara, v.TotalCents))
            .ToList();

        return new PainelDto(
            new PainelMesDto(
                de, ate,
                entregues.Count,
                entregues.Sum(v => v.TotalCents) / 100m,
                contas.Sum(c => c.IvaDaVenda),
                contas.Sum(c => c.IvaAPagarEstado),
                contas.Sum(c => c.Lucro),
                despesas.Sum(d => d.ValorCents) / 100m),
            new PainelEmCursoDto(
                emCurso.Count(v => v.Estado == VendaEstado.Orcamento),
                emCurso.Count(v => v.Estado == VendaEstado.EmCurso),
                emCurso.Count(v => v.Estado == VendaEstado.AEsperaPeca),
                emCurso.Count(v => v.Estado == VendaEstado.Pronta)),
            new PainelAlertasDto(totalPorFaturar, resumoCompras.DocumentosComFaturaEmFalta, resumoCompras.DocumentosComDiferenca, pendentes),
            new PainelStockDto(resumoCompras.EmStock.Unidades, resumoCompras.EmStock.ValorPecasPago, resumoCompras.EmStock.Lucro),
            proximas);
    }
}
