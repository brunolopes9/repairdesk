using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class CalendarioExternoRepository : ICalendarioExternoRepository
{
    private readonly AppDbContext _db;
    public CalendarioExternoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CalendarioExterno>> DoUtilizadorAsync(Guid userId, CancellationToken ct = default)
        => await _db.CalendariosExternos.IgnoreQueryFilters().Where(c => !c.IsDeleted && c.UserId == userId).OrderBy(c => c.Nome).ToListAsync(ct);

    public Task<CalendarioExterno?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.CalendariosExternos.IgnoreQueryFilters().FirstOrDefaultAsync(c => !c.IsDeleted && c.Id == id, ct);

    public async Task AddAsync(CalendarioExterno c, CancellationToken ct = default) => await _db.CalendariosExternos.AddAsync(c, ct);

    public void Remove(CalendarioExterno c) => _db.CalendariosExternos.Remove(c);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
