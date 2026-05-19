using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class PushSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }

    public DateTime? LastSentAt { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public string? LastError { get; set; }
}
