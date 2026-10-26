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
    /// <summary>Slug do portal do cliente (/r/{slug}) — gerado ao criar uma reparação.</summary>
    public string? PublicSlug { get; set; }
    /// <summary>Previsão de entrega mostrada ao cliente no portal enquanto está em curso.</summary>
    public DateTime? PrevistoPara { get; set; }
    public List<VendaEstadoLog> Timeline { get; set; } = new();

    /// <summary>Nº da fatura emitida fora do Mender (ex.: Moloni web) — registo manual.</summary>
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceEmittedAt { get; set; }

    /// <summary>Sprint 565: PDF da fatura emitida (Moloni), guardado no storage — o que vai para o contabilista.</summary>
    public string? FaturaPdfPath { get; set; }
    /// <summary>SHA-256 do PDF — o mesmo ficheiro carregado duas vezes é reconhecido.</summary>
    public string? FaturaPdfSha256 { get; set; }

    // Sprint 567: documento emitido no Moloni ON a partir do Mender.
    /// <summary>"invoice" (fatura, fica por pagar até ao recibo) ou "invoiceReceipt" (fatura-recibo, paga).</summary>
    public string? MoloniDocumentoTipo { get; set; }
    public int? MoloniDocumentoId { get; set; }
    public int? MoloniReciboId { get; set; }
    public string? MoloniReciboNumero { get; set; }
    public int? MoloniOrcamentoId { get; set; }
    public string? MoloniOrcamentoNumero { get; set; }

    public string? Notas { get; set; }
    public List<VendaItem> Items { get; set; } = new();
}
