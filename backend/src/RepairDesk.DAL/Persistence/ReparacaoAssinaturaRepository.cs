using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public sealed class ReparacaoAssinaturaRepository : IReparacaoAssinaturaRepository
{
    private readonly AppDbContext _db;
    public ReparacaoAssinaturaRepository(AppDbContext db) => _db = db;

    public Task<ReparacaoAssinatura?> FindAsync(Guid reparacaoId, AssinaturaTipo tipo, CancellationToken ct = default)
        => _db.ReparacaoAssinaturas.FirstOrDefaultAsync(a => a.ReparacaoId == reparacaoId && a.Tipo == tipo, ct);

    public async Task<IReadOnlyList<ReparacaoAssinatura>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.ReparacaoAssinaturas
            .AsNoTracking()
            .Where(a => a.ReparacaoId == reparacaoId)
            .OrderBy(a => a.Tipo)
            .ToListAsync(ct);

    public Task AddAsync(ReparacaoAssinatura assinatura, CancellationToken ct = default)
        => _db.ReparacaoAssinaturas.AddAsync(assinatura, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
