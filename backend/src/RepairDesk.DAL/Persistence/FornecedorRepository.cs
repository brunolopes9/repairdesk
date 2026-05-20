using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class FornecedorRepository : IFornecedorRepository
{
    private readonly AppDbContext _db;
    public FornecedorRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Fornecedor>> ListByTenantAsync(bool includeInactive, CancellationToken ct = default)
        => await _db.Fornecedores
            .AsNoTracking()
            .Where(f => includeInactive || f.Active)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    public Task<Fornecedor?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<Fornecedor?> FindByNameAsync(string name, CancellationToken ct = default)
        => _db.Fornecedores.FirstOrDefaultAsync(f => f.Name == name, ct);

    public Task AddAsync(Fornecedor f, CancellationToken ct = default)
        => _db.Fornecedores.AddAsync(f, ct).AsTask();

    public void Remove(Fornecedor f) => _db.Fornecedores.Remove(f);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
