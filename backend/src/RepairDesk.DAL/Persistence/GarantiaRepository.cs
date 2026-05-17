using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class GarantiaRepository : IGarantiaRepository
{
    private readonly AppDbContext _db;
    public GarantiaRepository(AppDbContext db) => _db = db;

    public Task<Garantia?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Garantias.FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <summary>Lookup público — sem filtro de tenant.</summary>
    public Task<Garantia?> FindBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Garantias
            .IgnoreQueryFilters()
            .Include(g => g.Reparacao)
            .ThenInclude(r => r!.Cliente)
            .Where(g => !g.IsDeleted)
            .FirstOrDefaultAsync(g => g.Slug == slug, ct);

    public Task<Garantia?> FindByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => _db.Garantias.FirstOrDefaultAsync(g => g.ReparacaoId == reparacaoId, ct);

    public Task AddAsync(Garantia g, CancellationToken ct = default)
        => _db.Garantias.AddAsync(g, ct).AsTask();

    public void Remove(Garantia g) => _db.Garantias.Remove(g);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class AvaliacaoRepository : IAvaliacaoRepository
{
    private readonly AppDbContext _db;
    public AvaliacaoRepository(AppDbContext db) => _db = db;

    public Task<Avaliacao?> FindByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => _db.Avaliacoes.FirstOrDefaultAsync(a => a.ReparacaoId == reparacaoId, ct);

    public async Task<IReadOnlyList<Avaliacao>> ListRecentesAsync(int take, CancellationToken ct = default)
        => await _db.Avaliacoes
            .AsNoTracking()
            .Include(a => a.Reparacao)
            .ThenInclude(r => r!.Cliente)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<(double? MediaScore, int Total)> EstatisticasAsync(CancellationToken ct = default)
    {
        var rows = await _db.Avaliacoes.AsNoTracking().Select(a => a.Score).ToListAsync(ct);
        if (rows.Count == 0) return (null, 0);
        return (rows.Average(), rows.Count);
    }

    public async Task<IReadOnlyDictionary<int, int>> DistribuicaoAsync(CancellationToken ct = default)
    {
        var groups = await _db.Avaliacoes
            .AsNoTracking()
            .GroupBy(a => a.Score)
            .Select(g => new { Score = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var dict = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        foreach (var g in groups)
        {
            if (g.Score >= 1 && g.Score <= 5) dict[g.Score] = g.Count;
        }
        return dict;
    }

    public Task AddAsync(Avaliacao a, CancellationToken ct = default)
        => _db.Avaliacoes.AddAsync(a, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
