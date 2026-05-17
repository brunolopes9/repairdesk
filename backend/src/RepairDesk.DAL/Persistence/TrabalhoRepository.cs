using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class TrabalhoRepository : ITrabalhoRepository
{
    private readonly AppDbContext _db;
    public TrabalhoRepository(AppDbContext db) => _db = db;

    public Task<Trabalho?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Trabalhos.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task CreateWithNextNumeroAsync(Trabalho trabalho, Guid tenantId, CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var max = await _db.Trabalhos
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId)
                .Select(t => (int?)t.Numero)
                .MaxAsync(ct);
            trabalho.Numero = (max ?? 0) + 1;
            _db.Trabalhos.Add(trabalho);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                _db.Entry(trabalho).State = EntityState.Detached;
            }
        }
    }

    public async Task<(IReadOnlyList<Trabalho> Items, int Total)> SearchAsync(
        string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Trabalhos.AsNoTracking().Include(t => t.Cliente).AsQueryable();
        if (status is not null) q = q.Where(t => t.Status == status.Value);
        if (categoria is not null) q = q.Where(t => t.Categoria == categoria.Value);
        if (clienteId is not null) q = q.Where(t => t.ClienteId == clienteId.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(t =>
                EF.Functions.Like(t.Titulo, like) ||
                (t.Descricao != null && EF.Functions.Like(t.Descricao, like)) ||
                (t.Cliente != null && EF.Functions.Like(t.Cliente.Nome, like)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Remove(Trabalho trabalho) => _db.Trabalhos.Remove(trabalho);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
