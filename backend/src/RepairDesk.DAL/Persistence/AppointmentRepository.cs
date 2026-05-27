using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;

    public AppointmentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Appointment>> ListByRangeAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default)
    {
        var q = _db.Appointments.AsNoTracking().Where(a => a.ScheduledAt >= fromUtc && a.ScheduledAt < toUtc);
        if (status is { } s) q = q.Where(a => a.Status == s);
        return await q.OrderBy(a => a.ScheduledAt).ToListAsync(ct);
    }

    public Task<Appointment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => await _db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelado
                && a.ScheduledAt < toUtc
                && fromUtc < a.ScheduledAt.AddMinutes(a.DurationMin))
            .ToListAsync(ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct = default)
        => await _db.Appointments.AddAsync(appointment, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
