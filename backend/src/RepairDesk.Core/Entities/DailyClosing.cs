using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): fecho de dia da caixa registadora (Z-report).
///
/// Cada loja faz fecho diário com:
/// - <see cref="OpeningCents"/>: saldo inicial declarado pelo operador
/// - <see cref="ExpectedClosingCents"/>: calculado (opening + entradas - saídas)
/// - <see cref="ActualClosingCents"/>: contado fisicamente pelo operador no fim
/// - <see cref="DiffCents"/>: diff (negativo = falta dinheiro, positivo = sobra)
///
/// Cumpre obrigação fiscal PT de "controlo de caixa" (DL 28/2019 art. 6.º).
/// Imprime-se o Z-report no fim do dia — PDF assinado armazenado.
///
/// **Estado:** Open quando criado. Closed quando operador faz fecho.
/// Uma vez Closed, NÃO se altera. Correções são novo dia ou ajuste manual com audit.
/// </summary>
public class DailyClosing : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? LocationId { get; set; }

    /// <summary>Dia ao qual este fecho diz respeito — UTC date (sem hora).</summary>
    public DateOnly Date { get; set; }

    public DailyClosingStatus Status { get; set; } = DailyClosingStatus.Open;

    /// <summary>Saldo declarado no início do dia (abertura). Em cents.</summary>
    public int OpeningCents { get; set; }

    /// <summary>
    /// Soma calculada: opening + Σ(entradas dinheiro) - Σ(saídas dinheiro).
    /// Outros métodos de pagamento (MBWay, cartão) contam para totais separados —
    /// não confundem com saldo físico da caixa.
    /// </summary>
    public int ExpectedClosingCents { get; set; }

    /// <summary>Contado fisicamente pelo operador. NULL enquanto Open.</summary>
    public int? ActualClosingCents { get; set; }

    /// <summary>ActualClosingCents - ExpectedClosingCents. Calculado no Close.</summary>
    public int? DiffCents { get; set; }

    /// <summary>Totais por método de pagamento — agregados para Z-report.</summary>
    public int CashEntriesCents { get; set; }
    public int CashExitsCents { get; set; }
    public int MbwayCents { get; set; }
    public int MultibancoCents { get; set; }
    public int CardCents { get; set; }
    public int OtherCents { get; set; }

    /// <summary>URL do PDF Z-report gerado no fecho (storage R2/local).</summary>
    public string? ZReportPdfUrl { get; set; }

    public DateTime? OpenedAt { get; set; }
    public Guid? OpenedByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }

    /// <summary>Notas livres do operador — justificar diff, observações do dia.</summary>
    public string? Notas { get; set; }
}
