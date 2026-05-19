using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Venda : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int Numero { get; set; }
    public DateTime Data { get; set; } = DateTime.UtcNow;

    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int TotalCents { get; set; }
    public int IvaCents { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Outro;
    public VendaStatus Status { get; set; } = VendaStatus.Pendente;
    /// <summary>Sprint 70: canal de origem da venda (default Balcao).</summary>
    public VendaOrigem Origem { get; set; } = VendaOrigem.Balcao;

    public BillingProvider InvoiceProvider { get; set; } = BillingProvider.None;
    public string? InvoiceExternalId { get; set; }
    public string? InvoicePdfUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceEmittedAt { get; set; }

    public string? Notas { get; set; }
    public List<VendaItem> Items { get; set; } = new();
}
