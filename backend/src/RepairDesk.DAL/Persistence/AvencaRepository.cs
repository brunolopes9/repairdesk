using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public sealed class AvencaRepository : IAvencaRepository
{
    private readonly AppDbContext _db;

    public AvencaRepository(AppDbContext db) => _db = db;

    public Task<Avenca?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Avencas.Include(a => a.Cliente).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Avenca>> ListAsync(Guid? clienteId, CancellationToken ct = default)
        => await _db.Avencas
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Where(a => clienteId == null || a.ClienteId == clienteId)
            .OrderBy(a => a.ProximaEmissao)
            .ToListAsync(ct);

    public async Task AddAsync(Avenca avenca, CancellationToken ct = default)
        => await _db.Avencas.AddAsync(avenca, ct);

    public void Remove(Avenca avenca) => _db.Avencas.Remove(avenca);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
