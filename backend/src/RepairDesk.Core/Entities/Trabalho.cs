using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Trabalho : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int Numero { get; set; }

    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public required string Titulo { get; set; }
    public string? Descricao { get; set; }
    public JobCategory Categoria { get; set; } = JobCategory.Outro;
    public TrabalhoStatus Status { get; set; } = TrabalhoStatus.Orcamento;

    public DateTime? DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }

    public int? OrcamentoCents { get; set; }
    public int? PrecoFinalCents { get; set; }
    public decimal HorasGastas { get; set; }

    public string? Notas { get; set; }
    public PaymentStatus EstadoPagamento { get; set; } = PaymentStatus.NaoPago;

    public BillingProvider InvoiceProvider { get; set; } = BillingProvider.None;
    public string? InvoiceExternalId { get; set; }
    public string? InvoicePdfUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceEmittedAt { get; set; }

    public string? EstimateExternalId { get; set; }
    public string? EstimateNumber { get; set; }
    public string? EstimatePdfUrl { get; set; }
    public DateTime? EstimateEmittedAt { get; set; }
}
