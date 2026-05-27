using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<Appointment>> ListByRangeAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default);
    Task<Appointment?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Agendamentos que se sobrepõem a um intervalo (para validar slots ocupados).</summary>
    Task<IReadOnlyList<Appointment>> ListOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
