using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ReparacaoFotoRepository : IReparacaoFotoRepository
{
    private readonly AppDbContext _db;
    public ReparacaoFotoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReparacaoFoto>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.ReparacaoFotos
            .Where(f => f.ReparacaoId == reparacaoId)
            .OrderBy(f => f.Tipo)
            .ThenBy(f => f.Ordem)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync(ct);

    public Task<ReparacaoFoto?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ReparacaoFotos.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<ReparacaoFoto>> ListPublicByReparacaoIdAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.ReparacaoFotos
            .IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && f.ReparacaoId == reparacaoId && f.VisivelNoPortal)
            .OrderBy(f => f.Tipo)
            .ThenBy(f => f.Ordem)
            .ToListAsync(ct);

    public Task AddAsync(ReparacaoFoto foto, CancellationToken ct = default)
        => _db.ReparacaoFotos.AddAsync(foto, ct).AsTask();

    public void Remove(ReparacaoFoto foto) => _db.ReparacaoFotos.Remove(foto);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
