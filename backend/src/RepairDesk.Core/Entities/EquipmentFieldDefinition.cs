using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class EquipmentFieldDefinition : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public EquipmentFieldTemplate? Template { get; set; }

    public required string Label { get; set; }
    public EquipmentFieldType Type { get; set; } = EquipmentFieldType.Text;
    public string? OptionsJson { get; set; }
    public bool Required { get; set; }
    public int Ordem { get; set; }
    public bool VisibleInPortal { get; set; } = true;

    public List<EquipmentFieldValue> Values { get; set; } = new();
}
