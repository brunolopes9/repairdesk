using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ApuramentoFechoRepository : IApuramentoFechoRepository
{
    private readonly AppDbContext _db;
    public ApuramentoFechoRepository(AppDbContext db) => _db = db;

    public Task<ApuramentoFecho?> FindAsync(int ano, int trimestre, CancellationToken ct = default)
        => _db.ApuramentoFechos.FirstOrDefaultAsync(f => f.Ano == ano && f.Trimestre == trimestre, ct);

    public Task AddAsync(ApuramentoFecho fecho, CancellationToken ct = default) => _db.ApuramentoFechos.AddAsync(fecho, ct).AsTask();
    public void Remove(ApuramentoFecho fecho) => _db.ApuramentoFechos.Remove(fecho);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
