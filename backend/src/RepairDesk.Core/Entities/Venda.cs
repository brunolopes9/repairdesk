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
    public VendaTipo Tipo { get; set; } = VendaTipo.Produto;
    public VendaEstado Estado { get; set; } = VendaEstado.Orcamento;
    /// <summary>Reparação: equipamento (marca, modelo, IMEI/nº série).</summary>
    public string? Equipamento { get; set; }
    /// <summary>Reparação: avaria descrita pelo cliente. Serviço: o que foi pedido.</summary>
    public string? Problema { get; set; }

    /// <summary>Nº da fatura emitida fora do Mender (ex.: Moloni web) — registo manual.</summary>
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceEmittedAt { get; set; }

    public string? Notas { get; set; }
    public List<VendaItem> Items { get; set; } = new();
}
