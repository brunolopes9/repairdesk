using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 353 (Doc 83 Pillar 5): conjunto pré-definido de peças aplicável
/// numa reparação numa selecção (ex: "Kit ecrã iPhone 13" = ecrã + adesivo
/// + parafusos). Reduz cliques no fluxo típico Bruno.
/// </summary>
public class PartKit : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    public string? Descricao { get; set; }
    public List<PartKitItem> Items { get; set; } = new();
}

public class PartKitItem : BaseEntity
{
    public Guid PartKitId { get; set; }
    public PartKit? PartKit { get; set; }

    public Guid PartId { get; set; }
    public Part? Part { get; set; }

    /// <summary>Quantidade desta peça no kit. Mínimo 1.</summary>
    public int Quantidade { get; set; } = 1;
}
