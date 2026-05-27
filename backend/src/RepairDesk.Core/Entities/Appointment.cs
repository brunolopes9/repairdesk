using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 371: agendamento (booking). O cliente marca hora para deixar o equipamento / um
/// serviço, ou o staff agenda no balcão/telefone. ClienteId é opcional (um booking online
/// pode chegar antes de o cliente existir na BD). Mantido como escalar (sem nav) de propósito
/// — evita armadilhas de mapeamento de relação (ver lição Sprint 362).
/// </summary>
public class Appointment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid? ClienteId { get; set; }

    public required string Nome { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Equipamento { get; set; }
    public string? Notas { get; set; }

    /// <summary>Início do agendamento (UTC).</summary>
    public DateTime ScheduledAt { get; set; }
    public int DurationMin { get; set; } = 30;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Agendado;
    public AppointmentSource Source { get; set; } = AppointmentSource.Balcao;
}

public enum AppointmentStatus
{
    Agendado = 0,
    Confirmado = 1,
    Concluido = 2,
    Cancelado = 3,
    NaoCompareceu = 4,
}

public enum AppointmentSource
{
    Balcao = 0,
    Online = 1,
}
