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

/// <summary>Vendas entregues num dia (para o gráfico dos últimos 30 dias).</summary>
public sealed record PainelDiaDto(DateTime Dia, int Vendas, decimal Faturado);

/// <summary>
/// Dashboard novo (18/11/2026): hoje, o mês até hoje comparado com o mesmo período do mês anterior,
/// os últimos 30 dias e as vendas em curso paradas há mais de <see cref="PainelService.DiasParada"/> dias.
/// </summary>
public sealed record PainelRitmoDto(
    int VendasHoje,
    decimal FaturadoHoje,
    decimal FaturadoMesAnteriorAteHoje,
    int Paradas,
    IReadOnlyList<PainelDiaDto> Ultimos30Dias);

public sealed record PainelDto(
    PainelMesDto Mes,
    PainelEmCursoDto EmCurso,
    PainelAlertasDto Alertas,
    PainelStockDto Stock,
    IReadOnlyList<PainelVendaResumoDto> ProximasEntregas,
    PainelRitmoDto? Ritmo = null);

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
    /// <summary>Uma venda em curso (que não está pronta) sem mexer há mais do que isto conta como parada.</summary>
    public const int DiasParada = 3;

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
        var contas = entregues.SelectMany(v => v.Items.Select(i => VendaService.Calcular(i, v.ForaApuramento))).ToList();

        var (despesas, _) = await _despesas.SearchAsync(null, null, null, false, false, de, ate.AddTicks(-1), null, 1, 10_000, ct, DespesaAmbito.Negocio);

        var (emCurso, _) = await _vendas.SearchAsync(new VendaFiltro(EmCurso: true), 1, 1000, ct);
        var (porFaturar, totalPorFaturar) = await _vendas.SearchAsync(new VendaFiltro(FaturaPorRegistar: true), 1, 1, ct);

        // Ritmo: hoje, mês anterior até ao mesmo dia e últimos 30 dias (dias em UTC, como o resto do painel).
        var hoje = agora.Date;
        var inicio30 = hoje.AddDays(-29);
        var deAnterior = de.AddMonths(-1);
        var desde = inicio30 < deAnterior ? inicio30 : deAnterior;
        var (recentes, _) = await _vendas.SearchAsync(new VendaFiltro(desde, hoje.AddDays(1), Estado: VendaEstado.Entregue), 1, MaxVendasMes * 2, ct);
        var ateAnterior = deAnterior.AddDays(Math.Min(agora.Day, DateTime.DaysInMonth(deAnterior.Year, deAnterior.Month)));
        var porDia = recentes.Where(v => v.Data >= inicio30).GroupBy(v => v.Data.Date).ToDictionary(g => g.Key, g => g.ToList());
        var ultimos30 = Enumerable.Range(0, 30).Select(i => inicio30.AddDays(i)).Select(d => porDia.TryGetValue(d, out var l)
            ? new PainelDiaDto(d, l.Count, l.Sum(v => v.TotalCents) / 100m)
            : new PainelDiaDto(d, 0, 0m)).ToList();
        var limiteParada = agora.AddDays(-DiasParada);
        var ritmo = new PainelRitmoDto(
            ultimos30[^1].Vendas,
            ultimos30[^1].Faturado,
            recentes.Where(v => v.Data >= deAnterior && v.Data < ateAnterior).Sum(v => v.TotalCents) / 100m,
            emCurso.Count(v => v.Estado != VendaEstado.Pronta && (v.UpdatedAt ?? v.CreatedAt) < limiteParada),
            ultimos30);

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
            proximas,
            ritmo);
    }
}
