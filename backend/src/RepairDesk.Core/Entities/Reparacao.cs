using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Reparacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int Numero { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public Guid? EquipmentFieldTemplateId { get; set; }
    public EquipmentFieldTemplate? EquipmentFieldTemplate { get; set; }

    public required string Equipamento { get; set; }
    /// <summary>
    /// Sprint 475 (Bruno braindump): categoria estruturada do equipamento (vs texto livre
    /// Equipamento). Nullable para back-compat com reparações antigas. Reusa o enum
    /// DeviceCategory já usado em PriceTable/Diagnostico/EquipmentFieldTemplate.
    /// </summary>
    public DeviceCategory? Categoria { get; set; }
    public string? Imei { get; set; }
    public required string Avaria { get; set; }
    public string? Diagnostico { get; set; }
    /// <summary>
    /// Sprint 474 (Bruno braindump Sergio A15): estado FÍSICO do equipamento quando recebido
    /// no balcão. Distinto de Diagnostico (que é o que técnico descobre DEPOIS).
    /// Tipicamente: "Ecrã rachado canto inferior direito · pequena mossa traseira · sem
    /// acessórios". Usado no comprovativo de entrada (S450) — fixa a inconsistência
    /// anterior que mostrava Diagnostico como estado físico inicial.
    /// </summary>
    public string? EstadoFisicoInicial { get; set; }

    public RepairStatus Estado { get; set; } = RepairStatus.Recebido;
    public DateTime EstadoSince { get; set; } = DateTime.UtcNow;

    public DateTime? EntregueEm { get; set; }

    /// <summary>
    /// Sprint 419: data/hora previstas de entrega ao cliente. Permite organizar reparações
    /// no calendário interno (caso típico: "telemóvel entrou hoje, peça vem em 2 dias,
    /// previsto entregar quinta 14h"). Nullable — só preenchido quando há ETA real.
    /// </summary>
    public DateTime? PrevistoEntregueEm { get; set; }

    public int? OrcamentoCents { get; set; }
    public bool OrcamentoAprovado { get; set; }

    public int? PrecoFinalCents { get; set; }
    public int CustoPecasCents { get; set; }
    public decimal HorasGastas { get; set; }

    public string? Notas { get; set; }
    public PaymentStatus EstadoPagamento { get; set; } = PaymentStatus.NaoPago;

    /// <summary>
    /// Sprint 499: sinal/depósito já recebido do cliente (cêntimos). Falta a pagar =
    /// (PrecoFinal ?? Orcamento) − SinalCents. 0 = sem sinal. Não é a caixa (decisão
    /// separada, como os pagamentos manuais): aqui só se regista o valor adiantado.
    /// </summary>
    public int SinalCents { get; set; }

    /// <summary>
    /// Sprint 343: técnico responsável por esta reparação. Null = não atribuída ainda.
    /// Quando Tech (role não-Admin) faz GET /reparacoes, filtra por AssignedToUserId == self.
    /// Admin vê todas, independente de owner.
    /// </summary>
    public Guid? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }

    public BillingProvider InvoiceProvider { get; set; } = BillingProvider.None;
    public string? InvoiceExternalId { get; set; }
    public string? InvoicePdfUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceEmittedAt { get; set; }

    // Sprint 528: recibo de liquidação emitido contra a fatura (a crédito). Quando preenchido, a
    // fatura está paga → o botão "Emitir recibo" desaparece e a ficha mostra o recibo.
    public string? ReciboNumero { get; set; }
    public DateTime? ReciboEmitidoEm { get; set; }

    public string? EstimateExternalId { get; set; }
    public string? EstimateNumber { get; set; }
    public string? EstimatePdfUrl { get; set; }
    public DateTime? EstimateEmittedAt { get; set; }

    /// <summary>
    /// Slug curto, único, alfanumérico (~8 chars) para portal cliente público.
    /// Gerado no Create. Usado em URLs /r/{slug} sem autenticação.
    /// </summary>
    public string? PublicSlug { get; set; }

    public List<ReparacaoEstadoLog> Timeline { get; set; } = new();
    public List<EquipmentFieldValue> EquipmentFieldValues { get; set; } = new();
    /// <summary>Sprint 346: tags categóricas (Urgente, Em garantia, etc).</summary>
    public List<ReparacaoTagAssignment> TagAssignments { get; set; } = new();
}
