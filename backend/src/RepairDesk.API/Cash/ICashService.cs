namespace RepairDesk.API.Cash;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): controlo de caixa diária (Z-report PT compliant).
///
/// Fluxo típico:
/// 1. <see cref="OpenDayAsync"/> — operador chega de manhã, declara saldo inicial físico
/// 2. Durante o dia: <see cref="RecordMovementAsync"/> regista cada entrada/saída
///    (chamado pelo VendaService quando se marca paga em dinheiro/MBWay/MB, manual para sangrias)
/// 3. <see cref="GetTodayAsync"/> mostra dashboard "Caixa hoje": saldo, entradas, saídas, totais por método
/// 4. <see cref="CloseDayAsync"/> — operador fecha caixa no fim do dia: conta dinheiro físico,
///    sistema calcula diff vs esperado, gera PDF Z-report.
///
/// **Idempotência:** OpenDay falha se já existe DailyClosing para (tenant, location, hoje).
/// CloseDay falha se status != Open. Imutabilidade depois de Closed.
/// </summary>
public interface ICashService
{
    /// <summary>Abre o dia. Falha se já aberto.</summary>
    Task<DailyClosingDto> OpenDayAsync(OpenDayRequest req, CancellationToken ct = default);

    /// <summary>Devolve fecho do dia actual (Open ou Closed). NULL se nunca aberto.</summary>
    Task<DailyClosingDto?> GetTodayAsync(Guid? locationId, CancellationToken ct = default);

    /// <summary>Devolve fecho específico (qualquer dia).</summary>
    Task<DailyClosingDto?> GetByDateAsync(DateOnly date, Guid? locationId, CancellationToken ct = default);

    /// <summary>Devolve fecho por Id (qualquer dia, qualquer location). NULL se não existe.</summary>
    Task<DailyClosingDto?> GetByIdAsync(Guid dailyClosingId, CancellationToken ct = default);

    /// <summary>
    /// Regista um movimento. Se for dinheiro entra no CashEntries do fecho aberto;
    /// se for MBWay/MB/cartão entra no total correspondente. Cria fecho automático
    /// se dia ainda não foi aberto (opening=0).
    /// </summary>
    Task<CashMovementDto> RecordMovementAsync(RecordMovementRequest req, CancellationToken ct = default);

    /// <summary>Lista N últimos fechos para tabela histórica.</summary>
    Task<IReadOnlyList<DailyClosingDto>> ListRecentAsync(int take, Guid? locationId, CancellationToken ct = default);

    /// <summary>Fecha o dia. Calcula diff entre actual e expected. Status → Closed.</summary>
    Task<DailyClosingDto> CloseDayAsync(Guid dailyClosingId, CloseDayRequest req, CancellationToken ct = default);
}
