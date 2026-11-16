using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class AlertaStockRepository : IAlertaStockRepository
{
    private readonly AppDbContext _db;
    public AlertaStockRepository(AppDbContext db) => _db = db;

    public Task<bool> ExisteAsync(string slug, string email, CancellationToken ct = default)
        => _db.AlertasStockLoja.AnyAsync(a => a.Slug == slug && a.Email == email, ct);

    public Task<int> ContarPorSlugAsync(string slug, CancellationToken ct = default)
        => _db.AlertasStockLoja.CountAsync(a => a.Slug == slug, ct);

    public async Task<IReadOnlyList<AlertaStockLoja>> DosSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken ct = default)
        => slugs.Count == 0 ? Array.Empty<AlertaStockLoja>()
            : await _db.AlertasStockLoja.Where(a => slugs.Contains(a.Slug)).OrderBy(a => a.CreatedAt).Take(500).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> ContagensAsync(CancellationToken ct = default)
        => await _db.AlertasStockLoja.GroupBy(a => a.Slug).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);

    public async Task AddAsync(AlertaStockLoja alerta, CancellationToken ct = default) => await _db.AlertasStockLoja.AddAsync(alerta, ct);
    public void Remove(AlertaStockLoja alerta) => _db.AlertasStockLoja.Remove(alerta);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
