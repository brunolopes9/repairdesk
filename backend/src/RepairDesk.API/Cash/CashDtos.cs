using RepairDesk.Core.Enums;

namespace RepairDesk.API.Cash;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): DTOs do POS / controlo de caixa.
/// Cents inteiros em todos os money fields — convenção do projecto.
/// </summary>

public sealed record OpenDayRequest(int OpeningCents, Guid? LocationId, string? Notas);

public sealed record DailyClosingDto(
    Guid Id,
    DateOnly Date,
    DailyClosingStatus Status,
    Guid? LocationId,
    int OpeningCents,
    int ExpectedClosingCents,
    int? ActualClosingCents,
    int? DiffCents,
    int CashEntriesCents,
    int CashExitsCents,
    int MbwayCents,
    int MultibancoCents,
    int CardCents,
    int OtherCents,
    string? ZReportPdfUrl,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    string? Notas,
    IReadOnlyList<CashMovementDto> Movimentos);

public sealed record CashMovementDto(
    Guid Id,
    CashMovementType Type,
    PaymentMethod PaymentMethod,
    int AmountCents,
    string Descricao,
    Guid? VendaId,
    Guid? ReparacaoId,
    DateTime OccurredAt);

public sealed record RecordMovementRequest(
    CashMovementType Type,
    PaymentMethod PaymentMethod,
    int AmountCents,
    string Descricao,
    Guid? VendaId,
    Guid? ReparacaoId,
    Guid? LocationId);

public sealed record CloseDayRequest(int ActualClosingCents, string? Notas);
