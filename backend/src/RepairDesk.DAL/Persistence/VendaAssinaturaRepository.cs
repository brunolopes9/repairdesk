using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public sealed class VendaAssinaturaRepository : IVendaAssinaturaRepository
{
    private readonly AppDbContext _db;
    public VendaAssinaturaRepository(AppDbContext db) => _db = db;

    public Task<VendaAssinatura?> FindAsync(Guid vendaId, AssinaturaTipo tipo, CancellationToken ct = default)
        => _db.VendaAssinaturas.FirstOrDefaultAsync(a => a.VendaId == vendaId && a.Tipo == tipo, ct);

    public async Task<IReadOnlyList<VendaAssinatura>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => await _db.VendaAssinaturas
            .AsNoTracking()
            .Where(a => a.VendaId == vendaId)
            .OrderBy(a => a.Tipo)
            .ToListAsync(ct);

    public Task AddAsync(VendaAssinatura assinatura, CancellationToken ct = default)
        => _db.VendaAssinaturas.AddAsync(assinatura, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
