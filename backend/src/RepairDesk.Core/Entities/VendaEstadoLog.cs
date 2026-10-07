using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>Histórico de estados de uma venda — alimenta a linha temporal do portal do cliente.</summary>
public class VendaEstadoLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VendaId { get; set; }
    public Venda? Venda { get; set; }
    public VendaEstado? EstadoFrom { get; set; }
    public VendaEstado EstadoTo { get; set; }
    public DateTime MudouEm { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
}
