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
    public string? Imei { get; set; }
    public required string Avaria { get; set; }
    public string? Diagnostico { get; set; }

    public RepairStatus Estado { get; set; } = RepairStatus.Recebido;
    public DateTime EstadoSince { get; set; } = DateTime.UtcNow;

    public DateTime? EntregueEm { get; set; }

    public int? OrcamentoCents { get; set; }
    public bool OrcamentoAprovado { get; set; }

    public int? PrecoFinalCents { get; set; }
    public int CustoPecasCents { get; set; }
    public decimal HorasGastas { get; set; }

    public string? Notas { get; set; }
    public PaymentStatus EstadoPagamento { get; set; } = PaymentStatus.NaoPago;

    /// <summary>
    /// Slug curto, único, alfanumérico (~8 chars) para portal cliente público.
    /// Gerado no Create. Usado em URLs /r/{slug} sem autenticação.
    /// </summary>
    public string? PublicSlug { get; set; }

    public List<ReparacaoEstadoLog> Timeline { get; set; } = new();
    public List<EquipmentFieldValue> EquipmentFieldValues { get; set; } = new();
}
