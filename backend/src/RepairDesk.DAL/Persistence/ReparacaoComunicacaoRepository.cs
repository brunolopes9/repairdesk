using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 452: implementação EF do <see cref="IReparacaoComunicacaoRepository"/>.</summary>
public class ReparacaoComunicacaoRepository : IReparacaoComunicacaoRepository
{
    private readonly AppDbContext _db;
    public ReparacaoComunicacaoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReparacaoComunicacao>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.ReparacaoComunicacoes
            .AsNoTracking()
            .Where(c => c.ReparacaoId == reparacaoId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<ReparacaoComunicacao?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ReparacaoComunicacoes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<int> CountByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => _db.ReparacaoComunicacoes.CountAsync(c => c.ReparacaoId == reparacaoId, ct);

    public async Task AddAsync(ReparacaoComunicacao entry, CancellationToken ct = default)
        => await _db.ReparacaoComunicacoes.AddAsync(entry, ct);

    public void Remove(ReparacaoComunicacao entry) => _db.ReparacaoComunicacoes.Remove(entry);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
