using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IDashboardRepository
{
    Task<DashboardSnapshot> GetSnapshotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<FinanceiroSnapshot> GetFinanceiroAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<AlertasSnapshot> GetAlertasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MesFinanceiroRow>> GetTendenciaAsync(int mesesAtras, CancellationToken ct = default);
    Task<IReadOnlyList<ReparacaoTopRow>> GetTopReparacoesAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
}

public sealed record DashboardSnapshot(
    int ReceitaCentsMes,
    int DespesasCentsMes,
    int ReparacoesAbertas,
    int TrabalhosAbertos,
    int ReparacoesEntreguesMes,
    int TrabalhosConcluidosMes,
    IReadOnlyList<CategoriaTotal> ReceitaPorCategoria,
    IReadOnlyList<CategoriaTotal> DespesaPorCategoria,
    IReadOnlyList<TopClienteRow> TopClientes);

public sealed record CategoriaTotal(string Label, int Count, int TotalCents);
public sealed record TopClienteRow(Guid Id, string Nome, int TotalCents, int Trabalhos);

public sealed record FinanceiroSnapshot(
    int ReceitaRealizadaCents,
    int CustoImputadoCents,
    int LucroRealizadoCents,
    int ReceitaPendenteCents,
    int InvestimentoStockCents,
    IReadOnlyList<CategoriaFinanceiraRow> PorCategoria);

public sealed record CategoriaFinanceiraRow(
    string Label,
    int Count,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);

public sealed record AlertasSnapshot(
    IReadOnlyList<ItemPorCobrarRow> TrabalhosNaoPagos,
    IReadOnlyList<ItemPorCobrarRow> ReparacoesNaoPagas,
    IReadOnlyList<DespesaOrfaRow> DespesasOrfas,
    int TotalPorCobrarCents,
    int TotalDespesasOrfasCents);

public sealed record ItemPorCobrarRow(
    Guid Id,
    int Numero,
    string Titulo,
    string? ClienteNome,
    int ValorCents,
    DateTime? ConcluidoEm);

public sealed record DespesaOrfaRow(
    Guid Id,
    string Descricao,
    int Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor);

public sealed record MesFinanceiroRow(
    int Ano,
    int Mes,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);

public sealed record ReparacaoTopRow(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);
