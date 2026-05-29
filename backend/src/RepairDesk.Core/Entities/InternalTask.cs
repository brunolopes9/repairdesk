using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 422 (Doc 90 Tier 2 #7): tarefa interna — TODO list por utilizador.
///
/// Use cases típicos: "Fazer follow-up Sergio sobre Samsung A15", "Pedir bateria
/// iPhone 13 ao Tudo4Mobile", "Limpar bancada". Pode opcionalmente ligar a uma
/// <see cref="Reparacao"/> ou ficar standalone.
///
/// Nome "InternalTask" para não colidir com <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public class InternalTask : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    public DateTime? DueAt { get; set; }

    public InternalTaskStatus Status { get; set; } = InternalTaskStatus.Pendente;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Utilizador responsável (null = qualquer um do tenant pode resolver).</summary>
    public Guid? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>Opcional: tarefa associada a uma reparação específica.</summary>
    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }
}
