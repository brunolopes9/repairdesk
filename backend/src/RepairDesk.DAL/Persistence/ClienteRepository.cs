using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _db;

    public ClienteRepository(AppDbContext db) => _db = db;

    public Task<Cliente?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> NifExistsAsync(string nif, Guid? exceptId = null, CancellationToken ct = default)
        => _db.Clientes.AnyAsync(c => c.Nif == nif && (exceptId == null || c.Id != exceptId), ct);

    public Task<Cliente?> FindByNifAsync(string nif, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Nif == nif, ct);

    public Task<Cliente?> FindByTelefoneAsync(string telefoneNormalizado, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Telefone == telefoneNormalizado, ct);

    public async Task<(IReadOnlyList<Cliente> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Clientes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(c =>
                EF.Functions.Like(c.Nome, like) ||
                EF.Functions.Like(c.Telefone, like) ||
                (c.Email != null && EF.Functions.Like(c.Email, like)) ||
                (c.Nif != null && EF.Functions.Like(c.Nif, like)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _db.Clientes.AsNoTracking().AnyAsync(ct);

    public async Task<IReadOnlyList<Cliente>> ExportAllAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync(ct);

    public Task AddAsync(Cliente cliente, CancellationToken ct = default) => _db.Clientes.AddAsync(cliente, ct).AsTask();
    public void Remove(Cliente cliente) => _db.Clientes.Remove(cliente);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
