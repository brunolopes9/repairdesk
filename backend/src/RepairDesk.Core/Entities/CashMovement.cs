using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): movimento de caixa registadora.
///
/// Representa **qualquer** entrada ou saída de dinheiro físico/digital da caixa
/// de uma loja durante o dia. Ligado opcionalmente a uma venda (recibo) — ou
/// solto para sangrias e reforços.
///
/// **Multi-location:** <see cref="LocationId"/> é nullable hoje (Pillar A.1 só
/// tem 1 location implícita por tenant); fica como null. Quando Pillar C aterrar
/// (Sprint 320+), backfill põe a default location de cada tenant. Migration sem
/// breaking change.
///
/// **Imutabilidade:** após criado, NÃO é editável. Correções fazem-se via
/// movimento inverso (Sprint 47 pattern — preferir compensação a edição).
/// </summary>
public class CashMovement : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Liga este movimento ao fecho do dia em que ocorreu — calculado no Close.
    /// NULL enquanto o dia ainda está aberto.
    /// </summary>
    public Guid? DailyClosingId { get; set; }
    public DailyClosing? DailyClosing { get; set; }

    /// <summary>Tipo de movimento — entrada (venda, reforço) ou saída (despesa, sangria, troco).</summary>
    public CashMovementType Type { get; set; }

    /// <summary>
    /// Valor sempre POSITIVO em cêntimos. O sinal é dado pelo Type — Saida/Sangria são
    /// somados como negativos no cálculo de saldo. Evita ambiguidade de signed integer.
    /// </summary>
    public int AmountCents { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Dinheiro;

    /// <summary>Descrição livre — "Troco para cliente Sergio", "Sangria 50€ para banco", etc.</summary>
    public required string Descricao { get; set; }

    /// <summary>Para entradas que correspondem a vendas, liga ao recibo.</summary>
    public Guid? VendaId { get; set; }
    public Venda? Venda { get; set; }

    /// <summary>Para entradas de pagamento de reparações.</summary>
    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>UserId do operador (do AppUser). Audit log também regista.</summary>
    public Guid? RecordedByUserId { get; set; }
}
