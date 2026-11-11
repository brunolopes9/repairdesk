using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 619 (Doc 98 F4): calendários externos (iCal) de cada utilizador.</summary>
public interface ICalendarioExternoRepository
{
    Task<IReadOnlyList<CalendarioExterno>> DoUtilizadorAsync(Guid userId, CancellationToken ct = default);
    Task<CalendarioExterno?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CalendarioExterno c, CancellationToken ct = default);
    void Remove(CalendarioExterno c);
    Task SaveAsync(CancellationToken ct = default);
}
