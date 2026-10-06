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

public sealed record AvisosPendentesResponse(
    IReadOnlyList<AvisoPendenteItem> Items,
    int TotalCount,
    int HorasLimite);

/// <summary>Sprint 467: Devices com garantia do fabricante a expirar nos próximos N dias.</summary>
public sealed record DevicesGarantiaAExpirarResponse(
    IReadOnlyList<DeviceGarantiaItem> Items,
    int TotalCount,
    int DiasJanela);

public sealed record DeviceGarantiaItem(
    Guid DeviceId,
    Guid ClienteId,
    string ClienteNome,
    string Tipo,
    string? Marca,
    string? Modelo,
    string? Apelido,
    string? Imei,
    DateOnly GarantiaFabricanteUntil);

/// <summary>
/// Sprint 460: reparação em estado comunicável (Diagnóstico/AguardaPeça/Pronto) há > N horas
/// sem comunicação Outbound desde a mudança de estado. Espelha a lógica do cron S458 mas
/// expõe ao frontend para widget Dashboard.
/// </summary>
public sealed record AvisoPendenteItem(
    Guid ReparacaoId,
    int Numero,
    int Estado,
    string Equipamento,
    string? ClienteNome,
    string? ClienteTelefone,
    DateTime EstadoSince,
    int HorasEmEstado);

/// <summary>
/// Sprint 483 (Doc 91): reparação cuja última mensagem no portal foi do cliente (Inbound
/// PortalCliente) e o staff ainda não respondeu pelo portal. Fecha o loop S480/S482 —
/// Bruno vê de relance quem está à espera de resposta.
/// </summary>
public sealed record MensagensPorResponderResponse(
    IReadOnlyList<MensagemPorResponderItem> Items,
    int TotalCount);

public sealed record MensagemPorResponderItem(
    Guid ReparacaoId,
    int Numero,
    int Estado,
    string Equipamento,
    string? ClienteNome,
    string UltimaMensagem,
    DateTime Em,
    int HorasEspera);

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

/// <summary>Sprint 429 (Doc 88 IDEIAS 1 + Doc 90 secção 3): série diária de cash flow para gráfico Dashboard.</summary>
public sealed record CashflowResponse(IReadOnlyList<CashflowDay> Days);

/// <summary>
/// Cash in vs cash out por dia. Receita = Reparações/Trabalhos pagos no dia (PrecoFinal)
/// + Vendas pagas no dia. Despesa = todas as Despesas com Data nesse dia (overhead + COGS).
/// Net = Receita - Despesa.
/// </summary>
public sealed record CashflowDay(
    DateTime Date,
    int ReceitaCents,
    int DespesaCents,
    int NetCents);

public sealed record TopReparacoesResponse(
    IReadOnlyList<ReparacaoTop> Items);

public sealed record GarantiasResumoResponse(
    int Activas,
    int ExpiramEm30Dias,
    int ExpiraramHoje,
    int Anuladas,
    IReadOnlyList<GarantiaProximaExpirarDto> ProximasAExpirar);

public sealed record ReparacoesEmGarantiaResponse(
    int TotalReparacoes,
    int TotalEntregues,
    int TotalPorcento,  // % do total de reparações no período
    int ValorOrcamentoCents,  // soma orçamentos das reparações em garantia interna
    IReadOnlyList<ReparacaoEmGarantiaDto> Itens);

public sealed record ReparacaoEmGarantiaDto(
    Guid ReparacaoId,
    int ReparacaoNumero,
    DateTime RecebidoEm,
    string Equipamento,
    string Imei,
    Guid VendaId,
    int VendaNumero,
    DateTime VendaData,
    string? ClienteNome,
    int? OrcamentoCents);

public sealed record GarantiaProximaExpirarDto(
    Guid Id,
    string Slug,
    DateTime DataFim,
    int DiasRestantes,
    string Origem,
    string? DocumentoReferencia,
    string? EquipamentoOuArtigo,
    string? ClienteNome,
    string? ClienteTelefone);

public sealed record ReparacaoTop(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoCents,
    int LucroCents);
