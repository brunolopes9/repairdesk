using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Garantia digital emitida automaticamente quando a reparação é
/// entregue. URL pública /g/{slug} permite ao cliente verificar
/// validade sem login.
/// </summary>
public class Garantia : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>Slug curto público para URL /g/{slug}. Distinto do PublicSlug da reparação.</summary>
    public required string Slug { get; set; }

    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime DataFim { get; set; }
    public int DiasGarantia { get; set; } = 90;

    /// <summary>Cobertura textual: o que está coberto ("Apenas o serviço prestado e peças substituídas").</summary>
    public string? Cobertura { get; set; }
    public string? Exclusoes { get; set; }

    /// <summary>Anulada manualmente (ex: cliente abriu equipamento).</summary>
    public bool Anulada { get; set; }
    public string? MotivoAnulacao { get; set; }
}
