using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class VisitasSiteRepository : IVisitasSiteRepository
{
    private readonly AppDbContext _db;
    public VisitasSiteRepository(AppDbContext db) => _db = db;

    public Task AdicionarAsync(IEnumerable<EventoSite> eventos, CancellationToken ct = default)
        => _db.EventosSite.AddRangeAsync(eventos, ct);

    public async Task<IReadOnlyList<EventoSite>> DoPeriodoAsync(DateTime deUtc, DateTime ateUtc, int max, CancellationToken ct = default)
        => await _db.EventosSite.AsNoTracking()
            .Where(e => e.CreatedAt >= deUtc && e.CreatedAt < ateUtc)
            .OrderBy(e => e.CreatedAt).Take(max).ToListAsync(ct);

    public async Task<IReadOnlyList<EventoSite>> DaSessaoAsync(string sessao, CancellationToken ct = default)
        => await _db.EventosSite.AsNoTracking().Where(e => e.Sessao == sessao).OrderBy(e => e.CreatedAt).Take(2000).ToListAsync(ct);

    public Task<int> ApagarDoVisitanteAsync(string visitante, CancellationToken ct = default)
        => _db.EventosSite.Where(e => e.Visitante == visitante).ExecuteDeleteAsync(ct);

    public Task<int> ApagarAntesDeAsync(DateTime antesUtc, CancellationToken ct = default)
        => _db.EventosSite.IgnoreQueryFilters().Where(e => e.CreatedAt < antesUtc).ExecuteDeleteAsync(ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
