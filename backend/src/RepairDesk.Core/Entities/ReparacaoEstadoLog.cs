using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class ReparacaoEstadoLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public RepairStatus? EstadoFrom { get; set; }
    public RepairStatus EstadoTo { get; set; }
    public DateTime MudouEm { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string? Notas { get; set; }
}
