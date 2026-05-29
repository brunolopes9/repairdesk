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

    /// <summary>
    /// Sprint 433 (Doc 90 §7.3 Bundles): mão-de-obra incluída no bundle (ex.: "Troca ecrã iPhone 13" =
    /// peças + €40 de serviço). Quando null/0, o kit é só peças e o preço é a soma do custo dos items.
    /// </summary>
    public int MaoDeObraCents { get; set; }
    public string? MaoDeObraDescricao { get; set; }

    /// <summary>
    /// Sprint 433: preço fixo do bundle ao cliente (override). Quando null usa a soma dos items
    /// + MaoDeObraCents. Quando definido, é o preço comercial fechado (margem implícita).
    /// </summary>
    public int? PrecoFinalCents { get; set; }
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
