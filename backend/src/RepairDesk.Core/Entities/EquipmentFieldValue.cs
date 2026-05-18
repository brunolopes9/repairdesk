using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class EquipmentFieldValue : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }
    public Guid FieldDefinitionId { get; set; }
    public EquipmentFieldDefinition? FieldDefinition { get; set; }
    public string? Value { get; set; }
}
