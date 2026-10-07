using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class VendaFotoRepository : IVendaFotoRepository
{
    private readonly AppDbContext _db;
    public VendaFotoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<VendaFoto>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => await _db.VendaFotos
            .Where(f => f.VendaId == vendaId)
            .OrderBy(f => f.Tipo)
            .ThenBy(f => f.Ordem)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync(ct);

    public Task<VendaFoto?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.VendaFotos.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<VendaFoto>> ListPublicByVendaIdAsync(Guid vendaId, CancellationToken ct = default)
        => await _db.VendaFotos
            .IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && f.VendaId == vendaId && f.VisivelNoPortal)
            .OrderBy(f => f.Tipo)
            .ThenBy(f => f.Ordem)
            .ToListAsync(ct);

    public Task AddAsync(VendaFoto foto, CancellationToken ct = default)
        => _db.VendaFotos.AddAsync(foto, ct).AsTask();

    public void Remove(VendaFoto foto) => _db.VendaFotos.Remove(foto);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
