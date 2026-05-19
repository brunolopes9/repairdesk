using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Dashboard;

public sealed record DashboardKpis(
    int ReceitaCentsMes,
    int DespesasCentsMes,
    int LucroCentsMes,
    int VendasHojeCents,
    int VendasMesCents,
    int ReparacoesAbertas,
    int TrabalhosAbertos,
    int ReparacoesEntreguesMes,
    int TrabalhosConcluidosMes);

public sealed record CategoriaBreakdown(string Label, int Count, int TotalCents);

public sealed record TopCliente(Guid Id, string Nome, int TotalCents, int Trabalhos);
public sealed record TopProdutoVendido(Guid? PartId, string Descricao, int Quantidade, int TotalCents);

public sealed record DashboardResponse(
    DashboardKpis Kpis,
    IReadOnlyList<CategoriaBreakdown> ReceitaPorCategoria,
    IReadOnlyList<CategoriaBreakdown> DespesaPorCategoria,
    IReadOnlyList<TopCliente> TopClientes,
    IReadOnlyList<TopProdutoVendido> TopProdutosVendidos);

public sealed record FinanceiroResponse(
    int ReceitaRealizadaCents,
    int CustoImputadoCents,
    int LucroRealizadoCents,
    int ReceitaPendenteCents,
    int InvestimentoStockCents,
    IReadOnlyList<CategoriaFinanceira> PorCategoria,
    DateTime PeriodoDe,
    DateTime PeriodoAte);

public sealed record CategoriaFinanceira(
    string Label,
    int Count,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);

public sealed record AlertasResponse(
    IReadOnlyList<ItemPorCobrar> TrabalhosNaoPagos,
    IReadOnlyList<ItemPorCobrar> ReparacoesNaoPagas,
    IReadOnlyList<DespesaOrfa> DespesasOrfas,
    int TotalPorCobrarCents,
    int TotalDespesasOrfasCents);

public sealed record ItemPorCobrar(
    Guid Id,
    int Numero,
    string Titulo,
    string? ClienteNome,
    int ValorCents,
    DateTime? ConcluidoEm);

public sealed record DespesaOrfa(
    Guid Id,
    string Descricao,
    int Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor);

public sealed record AvaliacoesDashboardResponse(
    double? MediaScore,
    int Total,
    IReadOnlyDictionary<int, int> Distribuicao,
    int Promoters,        // 5 estrelas
    int Detractors,       // 1-2 estrelas
    int Nps,              // (% promoters) - (% detractors) — clamp -100..100
    IReadOnlyList<AvaliacaoRecenteDto> Recentes);

public sealed record AvaliacaoRecenteDto(
    Guid Id,
    Guid ReparacaoId,
    int ReparacaoNumero,
    string ClienteNome,
    string Equipamento,
    int Score,
    string? Comentario,
    DateTime CriadaEm);

public sealed record TendenciaResponse(
    IReadOnlyList<MesFinanceiro> Meses);

public sealed record MesFinanceiro(
    int Ano,
    int Mes,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);

public sealed record TopReparacoesResponse(
    IReadOnlyList<ReparacaoTop> Items);

public sealed record ReparacaoTop(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);
