using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 366: subscrição de Web Push de um DISPOSITIVO de staff (telemóvel/desktop do
/// dono ou técnico). Distinta de <see cref="PushSubscription"/>, que é do CLIENTE e está
/// amarrada a uma reparação. Aqui o alvo é o utilizador/tenant — para o avisar de eventos
/// internos (pedido online novo, venda, stock baixo, reparação parada).
/// </summary>
public class StaffPushSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Utilizador (staff) dono deste dispositivo.</summary>
    public Guid UserId { get; set; }

    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }

    public DateTime? LastSentAt { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public string? LastError { get; set; }
}
