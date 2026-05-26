using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 349 (Doc 83 Pillar 6): time tracking de uma sessão de trabalho do
/// técnico numa reparação. Quando <see cref="EndedAt"/> é null o timer está
/// activo (em curso) — só pode haver um timer activo por (TenantId, UserId).
/// </summary>
public class ReparacaoTimeEntry : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>Técnico que está/esteve a trabalhar (FK para AppUser).</summary>
    public Guid UserId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Nota livre (ex: "Soldagem do conector", "À espera da peça").</summary>
    public string? Notas { get; set; }

    /// <summary>Duração em minutos (lazy-computed só quando EndedAt está definido).</summary>
    public int? DuracaoMinutos =>
        EndedAt is null ? null : (int)Math.Round((EndedAt.Value - StartedAt).TotalMinutes);
}
