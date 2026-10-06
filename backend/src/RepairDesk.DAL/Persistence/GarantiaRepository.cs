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

    public Task<Garantia?> FindByIdWithSourceAsync(Guid id, CancellationToken ct = default)
        => _db.Garantias
            .Include(g => g.Reparacao)
                .ThenInclude(r => r!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Items)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    /// <summary>Lookup público — sem filtro de tenant. Carrega Reparação OU Venda conforme origem.</summary>
    public Task<Garantia?> FindBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Garantias
            .IgnoreQueryFilters()
            .Include(g => g.Reparacao)
                .ThenInclude(r => r!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Items)
            .Where(g => !g.IsDeleted)
            .FirstOrDefaultAsync(g => g.Slug == slug, ct);

    public Task<Garantia?> FindByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => _db.Garantias.FirstOrDefaultAsync(g => g.ReparacaoId == reparacaoId, ct);

    public Task<Garantia?> FindByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => _db.Garantias.FirstOrDefaultAsync(g => g.VendaId == vendaId, ct);

    public async Task<GarantiasResumoRow> GetResumoAsync(DateTime agora, int diasJanela, int topLimit, CancellationToken ct = default)
    {
        var fimJanela = agora.AddDays(diasJanela);
        var hojeFim = agora.Date.AddDays(1);

        // Counters base (todas as garantias do tenant — filter automático via query filter)
        var todas = _db.Garantias.AsNoTracking().Where(g => !g.Anulada);
        var activas = await todas.CountAsync(g => g.DataInicio <= agora && g.DataFim >= agora, ct);
        var expiramEmJanela = await todas.CountAsync(g => g.DataFim >= agora && g.DataFim <= fimJanela, ct);
        var expiraramHoje = await todas.CountAsync(g => g.DataFim >= agora.Date && g.DataFim < hojeFim, ct);
        var anuladas = await _db.Garantias.AsNoTracking().CountAsync(g => g.Anulada, ct);

        // Top próximas a expirar — inclui dados de origem (Reparacao ou Venda) e cliente
        var proximas = await _db.Garantias.AsNoTracking()
            .Include(g => g.Reparacao)
                .ThenInclude(r => r!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Cliente)
            .Include(g => g.Venda)
                .ThenInclude(v => v!.Items)
            .Where(g => !g.Anulada && g.DataFim >= agora && g.DataFim <= fimJanela)
            .OrderBy(g => g.DataFim)
            .Take(topLimit)
            .ToListAsync(ct);

        var rows = proximas.Select(g =>
        {
            var dias = (int)Math.Max(0, (g.DataFim - agora).TotalDays);
            if (g.VendaId is not null && g.Venda is not null)
            {
                var primeiro = g.Venda.Items.FirstOrDefault()?.Descricao;
                return new GarantiaProximaExpirarRow(
                    g.Id, g.Slug, g.DataFim, dias,
                    "Venda", $"Venda #{g.Venda.Numero:D5}",
                    primeiro ?? "Artigos vendidos",
                    g.Venda.Cliente?.Nome,
                    g.Venda.Cliente?.Telefone);
            }
            return new GarantiaProximaExpirarRow(
                g.Id, g.Slug, g.DataFim, dias,
                "Reparacao",
                g.Reparacao is not null ? $"Reparação #{g.Reparacao.Numero:D5}" : null,
                g.Reparacao?.Equipamento,
                g.Reparacao?.Cliente?.Nome,
                g.Reparacao?.Cliente?.Telefone);
        }).ToList();

        return new GarantiasResumoRow(activas, expiramEmJanela, expiraramHoje, anuladas, rows);
    }

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
