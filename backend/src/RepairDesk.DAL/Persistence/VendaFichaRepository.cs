using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public sealed class VendaFichaRepository : IVendaFichaRepository
{
    private readonly AppDbContext _db;
    public VendaFichaRepository(AppDbContext db) => _db = db;

    public Task<VendaFicha?> FindByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => _db.VendaFichas.FirstOrDefaultAsync(f => f.VendaId == vendaId, ct);

    public Task AddAsync(VendaFicha ficha, CancellationToken ct = default)
        => _db.VendaFichas.AddAsync(ficha, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
