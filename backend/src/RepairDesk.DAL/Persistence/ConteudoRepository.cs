using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 598 (Doc 95): implementação EF do <see cref="IConteudoRepository"/>.</summary>
public class ConteudoRepository : IConteudoRepository
{
    private readonly AppDbContext _db;

    public ConteudoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConteudoPublicacao>> ListPublicacoesAsync(
        IReadOnlyCollection<ConteudoEstado>? estados,
        DateTime? deUtc,
        DateTime? ateUtc,
        CancellationToken ct = default)
    {
        var q = _db.ConteudoPublicacoes.AsNoTracking()
            .Include(p => p.Ficheiros.OrderBy(f => f.Ordem))
            .Include(p => p.Destinos)
            .AsSplitQuery()
            .AsQueryable();
        if (estados is { Count: > 0 }) q = q.Where(p => estados.Contains(p.Estado));
        if (deUtc is { } de) q = q.Where(p => p.AgendadoPara == null || p.AgendadoPara >= de);
        if (ateUtc is { } ate) q = q.Where(p => p.AgendadoPara == null || p.AgendadoPara < ate);
        // Sem hora no fim; o resto por hora de publicação.
        return await q
            .OrderBy(p => p.AgendadoPara == null ? 1 : 0)
            .ThenBy(p => p.AgendadoPara)
            .ThenBy(p => p.Numero)
            .Take(500)
            .ToListAsync(ct);
    }

    public Task<ConteudoPublicacao?> FindPublicacaoAsync(Guid id, CancellationToken ct = default)
        => _db.ConteudoPublicacoes
            .Include(p => p.Ficheiros.OrderBy(f => f.Ordem))
            .Include(p => p.Destinos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<int> CountPorAprovarAsync(CancellationToken ct = default)
        => _db.ConteudoPublicacoes.CountAsync(p => p.Estado == ConteudoEstado.PorAprovar, ct);

    public async Task<IReadOnlyList<ConteudoFicheiro>> ListBibliotecaAsync(bool soPorProcessar, CancellationToken ct = default)
    {
        var q = _db.ConteudoFicheiros.AsNoTracking()
            .Where(f => f.PublicacaoId == null && f.Estado == ConteudoFicheiroEstado.Pronto);
        if (soPorProcessar) q = q.Where(f => f.ProcessadoEm == null);
        return await q.OrderByDescending(f => f.CreatedAt).Take(300).ToListAsync(ct);
    }

    public Task<ConteudoFicheiro?> FindFicheiroAsync(Guid id, CancellationToken ct = default)
        => _db.ConteudoFicheiros.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<ConteudoFicheiro>> FindFicheirosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => ids.Count == 0
            ? Array.Empty<ConteudoFicheiro>()
            : await _db.ConteudoFicheiros.Where(f => ids.Contains(f.Id)).ToListAsync(ct);

    public async Task AddAsync(ConteudoPublicacao publicacao, CancellationToken ct = default)
        => await _db.ConteudoPublicacoes.AddAsync(publicacao, ct);

    public async Task AddAsync(ConteudoFicheiro ficheiro, CancellationToken ct = default)
        => await _db.ConteudoFicheiros.AddAsync(ficheiro, ct);

    public void Remove(ConteudoPublicacao publicacao) => _db.ConteudoPublicacoes.Remove(publicacao);
    public void Remove(ConteudoFicheiro ficheiro) => _db.ConteudoFicheiros.Remove(ficheiro);
    public void Remove(ConteudoDestino destino) => _db.ConteudoDestinos.Remove(destino);

    /// <summary>
    /// Grava e atribui o nº visível (por tenant) às publicações novas. Dois pedidos em simultâneo
    /// podem calcular o mesmo nº: o índice único rejeita um deles e tenta-se outra vez (como nas despesas).
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var novas = _db.ChangeTracker.Entries<ConteudoPublicacao>()
                .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
            // O TenantId só é carimbado no SaveChanges — aqui usa-se já o do contexto para contar no tenant certo.
            foreach (var p in novas.Where(p => p.TenantId == Guid.Empty && _db.CurrentTenantId is not null)) p.TenantId = _db.CurrentTenantId!.Value;
            foreach (var grupo in novas.GroupBy(p => p.TenantId))
            {
                var max = await _db.ConteudoPublicacoes.IgnoreQueryFilters().Where(p => p.TenantId == grupo.Key)
                    .Select(p => (int?)p.Numero).MaxAsync(ct) ?? 0;
                foreach (var p in grupo.OrderBy(p => p.AgendadoPara ?? DateTime.MaxValue)) p.Numero = ++max;
            }
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && novas.Count > 0)
            {
                foreach (var p in novas) p.Numero = 0;
            }
        }
    }
}
