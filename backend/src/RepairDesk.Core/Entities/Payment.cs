using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 303: transacção de pagamento associada a uma <see cref="Venda"/>. Permite múltiplos
/// pagamentos parciais (ex: 50€ dinheiro + 30€ MBWay) e tracking de estado quando o pagamento
/// passa por um provider assíncrono como IFTHENPAY (MBWay push notification).
/// </summary>
public class Payment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Venda associada (POS). Sprint 493: nullable — um Payment pode agora estar ligado a uma
    /// <see cref="ReparacaoId"/> em vez de uma Venda (pagamento de reparação pelo portal cliente).
    /// Exactamente um de VendaId / ReparacaoId é preenchido (garantido pelo serviço).
    /// </summary>
    public Guid? VendaId { get; set; }
    public Venda? Venda { get; set; }

    /// <summary>Sprint 493: reparação paga via portal MBWay (alternativa a VendaId). Coluna simples,
    /// sem FK navegável — integridade garantida no serviço.</summary>
    public Guid? ReparacaoId { get; set; }

    public PaymentMethod Method { get; set; }
    public PaymentProvider Provider { get; set; } = PaymentProvider.Manual;

    /// <summary>Valor em cêntimos. Permite pagamentos parciais.</summary>
    public int AmountCents { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.NaoPago;

    /// <summary>
    /// Identificador devolvido pelo provider (ex: IFTHENPAY transaction ID, referência MB).
    /// Null para pagamentos Manual.
    /// </summary>
    public string? ProviderRef { get; set; }

    /// <summary>
    /// ID externo opcional (ex: ChavePagamentoIfthenpay para correlacionar webhook).
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>JSON com metadata específica do provider (referência MB, entidade, etc).</summary>
    public string? MetadataJson { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Quando o pagamento expira (relevante para MBWay push e refs MB).</summary>
    public DateTime? ExpiresAt { get; set; }

    public string? FailureReason { get; set; }
}
