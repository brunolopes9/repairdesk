using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 346 (Doc 83 Pillar 6): tag categórica aplicável a reparações.
/// Versátil — Bruno usa para indicar "Urgente", "Em garantia", "Prateleira 3",
/// tipo de avaria, etc. Many-to-many com <see cref="Reparacao"/>.
/// </summary>
public class ReparacaoTag : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Nome curto (max 40 chars). Único por tenant.</summary>
    public required string Nome { get; set; }

    /// <summary>Cor em formato hex (#RRGGBB). Default zinc-700.</summary>
    public string CorHex { get; set; } = "#3F3F46";

    public List<ReparacaoTagAssignment> Assignments { get; set; } = new();
}

/// <summary>Tabela de junção many-to-many entre Reparacao e ReparacaoTag.</summary>
public class ReparacaoTagAssignment : BaseEntity
{
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public Guid ReparacaoTagId { get; set; }
    public ReparacaoTag? ReparacaoTag { get; set; }
}
