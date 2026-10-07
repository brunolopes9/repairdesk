using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 452: implementação EF do <see cref="IVendaComunicacaoRepository"/>.</summary>
public class VendaComunicacaoRepository : IVendaComunicacaoRepository
{
    private readonly AppDbContext _db;
    public VendaComunicacaoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<VendaComunicacao>> ListByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => await _db.VendaComunicacoes
            .AsNoTracking()
            .Where(c => c.VendaId == vendaId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VendaComunicacao>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default)
        => await _db.VendaComunicacoes
            .AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public Task<VendaComunicacao?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.VendaComunicacoes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<int> CountByVendaAsync(Guid vendaId, CancellationToken ct = default)
        => _db.VendaComunicacoes.CountAsync(c => c.VendaId == vendaId, ct);

    public async Task AddAsync(VendaComunicacao entry, CancellationToken ct = default)
        => await _db.VendaComunicacoes.AddAsync(entry, ct);

    public void Remove(VendaComunicacao entry) => _db.VendaComunicacoes.Remove(entry);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
