using RepairDesk.Core.Abstractions;

namespace RepairDesk.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardResponse> GetCurrentMonthAsync(CancellationToken ct = default);
    Task<DashboardResponse> GetRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<FinanceiroResponse> GetFinanceiroCurrentMonthAsync(CancellationToken ct = default);
    Task<FinanceiroResponse> GetFinanceiroRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<AlertasResponse> GetAlertasAsync(CancellationToken ct = default);
    Task<TendenciaResponse> GetTendenciaAsync(int meses, CancellationToken ct = default);
    Task<TopReparacoesResponse> GetTopReparacoesAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
    Task<TopReparacoesResponse> GetTopReparacoesCurrentMonthAsync(int limit, CancellationToken ct = default);
    Task<AvaliacoesDashboardResponse> GetAvaliacoesAsync(CancellationToken ct = default);
    Task<GarantiasResumoResponse> GetGarantiasResumoAsync(int diasJanela, int topLimit, CancellationToken ct = default);
    Task<ReparacoesEmGarantiaResponse> GetReparacoesEmGarantiaAsync(int diasJanela, int topLimit, CancellationToken ct = default);
    Task<byte[]> ExportReparacoesEmGarantiaCsvAsync(int diasJanela, CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;
    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IGarantiaRepository _garantias;

    public DashboardService(IDashboardRepository repo, IAvaliacaoRepository avaliacoes, IGarantiaRepository garantias)
    {
        _repo = repo;
        _avaliacoes = avaliacoes;
        _garantias = garantias;
    }

    public async Task<GarantiasResumoResponse> GetGarantiasResumoAsync(int diasJanela, int topLimit, CancellationToken ct = default)
    {
        diasJanela = Math.Clamp(diasJanela, 1, 365);
        topLimit = Math.Clamp(topLimit, 1, 50);
        var resumo = await _garantias.GetResumoAsync(DateTime.UtcNow, diasJanela, topLimit, ct);
        return new GarantiasResumoResponse(
            resumo.Activas,
            resumo.ExpiramEmJanela,
            resumo.ExpiraramHoje,
            resumo.Anuladas,
            resumo.ProximasAExpirar.Select(p => new GarantiaProximaExpirarDto(
                p.Id, p.Slug, p.DataFim, p.DiasRestantes, p.Origem,
                p.DocumentoReferencia, p.EquipamentoOuArtigo, p.ClienteNome, p.ClienteTelefone)).ToList());
    }

    public async Task<ReparacoesEmGarantiaResponse> GetReparacoesEmGarantiaAsync(int diasJanela, int topLimit, CancellationToken ct = default)
    {
        diasJanela = Math.Clamp(diasJanela, 1, 365);
        topLimit = Math.Clamp(topLimit, 1, 100);
        var fromUtc = DateTime.UtcNow.AddDays(-diasJanela);
        var rows = await _repo.GetReparacoesEmGarantiaAsync(fromUtc, DateTime.UtcNow, topLimit, ct);

        var totalReparacoesNoPeriodo = await _repo.GetReparacoesCountAsync(fromUtc, DateTime.UtcNow, ct);
        var totalEntregues = rows.Items.Count(i => i.Entregue);
        var totalPct = totalReparacoesNoPeriodo == 0 ? 0
            : (int)Math.Round(rows.Total * 100.0 / totalReparacoesNoPeriodo);

        return new ReparacoesEmGarantiaResponse(
            rows.Total,
            totalEntregues,
            totalPct,
            rows.ValorOrcamentoCents,
            rows.Items.Select(i => new ReparacaoEmGarantiaDto(
                i.ReparacaoId, i.ReparacaoNumero, i.RecebidoEm, i.Equipamento, i.Imei,
                i.VendaId, i.VendaNumero, i.VendaData, i.ClienteNome, i.OrcamentoCents)).ToList());
    }

    public async Task<byte[]> ExportReparacoesEmGarantiaCsvAsync(int diasJanela, CancellationToken ct = default)
    {
        diasJanela = Math.Clamp(diasJanela, 1, 730);
        var fromUtc = DateTime.UtcNow.AddDays(-diasJanela);
        var rows = await _repo.GetReparacoesEmGarantiaAsync(fromUtc, DateTime.UtcNow, limit: 1000, ct);

        var csv = new Common.Helpers.CsvBuilder();
        csv.Row(
            "data_reparacao", "reparacao_numero", "equipamento", "imei",
            "venda_numero", "venda_data", "dias_entre_venda_e_reparacao",
            "cliente_nome", "orcamento_eur", "entregue");

        foreach (var r in rows.Items)
        {
            var dias = (int)Math.Round((r.RecebidoEm - r.VendaData).TotalDays);
            csv.Row(
                r.RecebidoEm.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                r.ReparacaoNumero,
                r.Equipamento,
                r.Imei,
                r.VendaNumero,
                r.VendaData.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                dias,
                r.ClienteNome ?? "",
                r.OrcamentoCents is { } c
                    ? (c / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    : "",
                r.Entregue ? "sim" : "nao");
        }
        return csv.ToUtf8WithBom();
    }

    public async Task<AvaliacoesDashboardResponse> GetAvaliacoesAsync(CancellationToken ct = default)
    {
        var (media, total) = await _avaliacoes.EstatisticasAsync(ct);
        var dist = await _avaliacoes.DistribuicaoAsync(ct);
        var recentesRows = await _avaliacoes.ListRecentesAsync(10, ct);

        var promoters = dist.GetValueOrDefault(5);
        var detractors = dist.GetValueOrDefault(1) + dist.GetValueOrDefault(2);
        var nps = total == 0 ? 0 : (int)Math.Round(((double)promoters - detractors) / total * 100);

        var recentes = recentesRows.Select(a => new AvaliacaoRecenteDto(
            a.Id,
            a.ReparacaoId,
            a.Reparacao?.Numero ?? 0,
            a.Reparacao?.Cliente?.Nome ?? "Cliente",
            a.Reparacao?.Equipamento ?? "Equipamento",
            a.Score,
            a.Comentario,
            a.CreatedAt)).ToList();

        return new AvaliacoesDashboardResponse(media, total, dist, promoters, detractors, nps, recentes);
    }

    public Task<DashboardResponse> GetCurrentMonthAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return GetRangeAsync(from, to, ct);
    }

    // Constructor antigo removido (era só repo). DashboardService usa agora repo + avaliacoes.

    public async Task<TendenciaResponse> GetTendenciaAsync(int meses, CancellationToken ct = default)
    {
        var rows = await _repo.GetTendenciaAsync(meses, ct);
        return new TendenciaResponse(
            rows.Select(r => new MesFinanceiro(r.Ano, r.Mes, r.ReceitaCents, r.CustoCents, r.LucroCents)).ToList());
    }

    public async Task<TopReparacoesResponse> GetTopReparacoesAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
    {
        var rows = await _repo.GetTopReparacoesAsync(fromUtc, toUtc, limit, ct);
        return new TopReparacoesResponse(
            rows.Select(r => new ReparacaoTop(r.Id, r.Numero, r.Equipamento, r.ClienteNome, r.ReceitaCents, r.CustoCents, r.LucroCents)).ToList());
    }

    public Task<TopReparacoesResponse> GetTopReparacoesCurrentMonthAsync(int limit, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return GetTopReparacoesAsync(from, to, limit, ct);
    }

    public async Task<AlertasResponse> GetAlertasAsync(CancellationToken ct = default)
    {
        var s = await _repo.GetAlertasAsync(ct);
        return new AlertasResponse(
            s.TrabalhosNaoPagos.Select(r => new ItemPorCobrar(r.Id, r.Numero, r.Titulo, r.ClienteNome, r.ValorCents, r.ConcluidoEm)).ToList(),
            s.ReparacoesNaoPagas.Select(r => new ItemPorCobrar(r.Id, r.Numero, r.Titulo, r.ClienteNome, r.ValorCents, r.ConcluidoEm)).ToList(),
            s.DespesasOrfas.Select(r => new DespesaOrfa(r.Id, r.Descricao, r.Categoria, r.ValorCents, r.Data, r.Fornecedor)).ToList(),
            s.TotalPorCobrarCents,
            s.TotalDespesasOrfasCents);
    }

    public Task<FinanceiroResponse> GetFinanceiroCurrentMonthAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return GetFinanceiroRangeAsync(from, to, ct);
    }

    public async Task<FinanceiroResponse> GetFinanceiroRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var s = await _repo.GetFinanceiroAsync(fromUtc, toUtc, ct);
        var cats = s.PorCategoria
            .Select(c => new CategoriaFinanceira(c.Label, c.Count, c.ReceitaCents, c.CustoCents, c.LucroCents))
            .ToList();
        return new FinanceiroResponse(
            s.ReceitaRealizadaCents,
            s.CustoImputadoCents,
            s.LucroRealizadoCents,
            s.ReceitaPendenteCents,
            s.InvestimentoStockCents,
            cats,
            fromUtc,
            toUtc);
    }

    public async Task<DashboardResponse> GetRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var s = await _repo.GetSnapshotAsync(fromUtc, toUtc, ct);
        var kpis = new DashboardKpis(
            s.ReceitaCentsMes,
            s.DespesasCentsMes,
            s.ReceitaCentsMes - s.DespesasCentsMes,
            s.VendasHojeCents,
            s.VendasMesCents,
            s.ReparacoesAbertas,
            s.TrabalhosAbertos,
            s.ReparacoesEntreguesMes,
            s.TrabalhosConcluidosMes);
        var topClientes = s.TopClientes
            .Select(t => new TopCliente(t.Id, t.Nome, t.TotalCents, t.Trabalhos))
            .ToList();
        var topProdutos = s.TopProdutosVendidos
            .Select(t => new TopProdutoVendido(t.PartId, t.Descricao, t.Quantidade, t.TotalCents))
            .ToList();
        var receita = s.ReceitaPorCategoria.Select(r => new CategoriaBreakdown(r.Label, r.Count, r.TotalCents)).ToList();
        var despesa = s.DespesaPorCategoria.Select(r => new CategoriaBreakdown(r.Label, r.Count, r.TotalCents)).ToList();
        return new DashboardResponse(kpis, receita, despesa, topClientes, topProdutos);
    }
}
