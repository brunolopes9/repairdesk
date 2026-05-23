using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IDashboardRepository
{
    Task<DashboardSnapshot> GetSnapshotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<DashboardKpisHojeSnapshot> GetKpisHojeAsync(DateTime diaUtc, CancellationToken ct = default);
    Task<FinanceiroSnapshot> GetFinanceiroAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<AlertasSnapshot> GetAlertasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MesFinanceiroRow>> GetTendenciaAsync(int mesesAtras, CancellationToken ct = default);
    Task<IReadOnlyList<ReparacaoTopRow>> GetTopReparacoesAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
    Task<int> GetReparacoesCountAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<ReparacoesEmGarantiaSnapshot> GetReparacoesEmGarantiaAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
}

public sealed record ReparacoesEmGarantiaSnapshot(
    int Total,
    int ValorOrcamentoCents,
    IReadOnlyList<ReparacaoEmGarantiaRow> Items);

public sealed record ReparacaoEmGarantiaRow(
    Guid ReparacaoId,
    int ReparacaoNumero,
    DateTime RecebidoEm,
    string Equipamento,
    string Imei,
    bool Entregue,
    int? OrcamentoCents,
    Guid VendaId,
    int VendaNumero,
    DateTime VendaData,
    string? ClienteNome);

public sealed record DashboardSnapshot(
    int ReceitaCentsMes,
    int DespesasCentsMes,
    int VendasHojeCents,
    int VendasMesCents,
    int ReparacoesAbertas,
    int TrabalhosAbertos,
    int ReparacoesEntreguesMes,
    int TrabalhosConcluidosMes,
    IReadOnlyList<CategoriaTotal> ReceitaPorCategoria,
    IReadOnlyList<CategoriaTotal> DespesaPorCategoria,
    IReadOnlyList<TopClienteRow> TopClientes,
    IReadOnlyList<TopProdutoVendidoRow> TopProdutosVendidos);

public sealed record CategoriaTotal(string Label, int Count, int TotalCents);
public sealed record TopClienteRow(Guid Id, string Nome, int TotalCents, int Trabalhos);
public sealed record TopProdutoVendidoRow(Guid? PartId, string Descricao, int Quantidade, int TotalCents);

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

public sealed record DashboardKpisHojeSnapshot(
    int ReparacoesEmCurso,
    int ValorAReceberCents,
    int StockCriticoCount,
    IReadOnlyList<int> Receita7d,
    int ReparacoesEntregues7d,
    int LucroEstimado7dCents,
    double? TempoMedioReparacaoHoras,
    IReadOnlyList<DashboardTopReparacaoLucrativaRow> TopReparacoesLucrativas30d,
    IReadOnlyList<DashboardTopPecaUsadaRow> TopPecasUsadas30d);

public sealed record DashboardTopReparacaoLucrativaRow(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoPecasCents,
    int LucroCents);

public sealed record DashboardTopPecaUsadaRow(
    Guid PartId,
    string Nome,
    string? Sku,
    int Quantidade);
