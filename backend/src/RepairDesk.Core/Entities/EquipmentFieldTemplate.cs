using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class EquipmentFieldTemplate : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    public DeviceCategory Categoria { get; set; } = DeviceCategory.Outro;
    public bool IsActive { get; set; } = true;
    public int Ordem { get; set; }

    public List<EquipmentFieldDefinition> Fields { get; set; } = new();
}
