using Microsoft.EntityFrameworkCore;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ArtigoRepository : IArtigoRepository
{
    private readonly AppDbContext _db;
    public ArtigoRepository(AppDbContext db) => _db = db;

    private IQueryable<Artigo> ComLotes() => _db.Artigos
        .Include(a => a.Lotes).ThenInclude(l => l.Documento).ThenInclude(d => d!.Fornecedor);

    public async Task<IReadOnlyList<Artigo>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default)
    {
        var query = ComLotes().AsNoTracking().Where(a => incluirInativos || a.Ativo);
        foreach (var termo in PesquisaTexto.Termos(q))
        {
            var t = termo.ToUpper();
            query = query.Where(a => a.Nome.ToUpper().Contains(t) || a.Sku.ToUpper().Contains(t) || a.CodigoBarras.Contains(t)
                || (a.CodigoFabricante != null && a.CodigoFabricante.Contains(t)) || (a.Categoria != null && a.Categoria.ToUpper().Contains(t)));
        }
        return await query.OrderBy(a => a.Nome).ToListAsync(ct);
    }

    public Task<Artigo?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => ComLotes().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Artigo?> FindByCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var c = codigo.Trim().ToUpper();
        return ComLotes().Where(a => a.CodigoBarras == c || a.CodigoFabricante == c || a.Sku.ToUpper() == c)
            .OrderByDescending(a => a.Ativo).FirstOrDefaultAsync(ct);
    }

    public Task<bool> CodigoEmUsoAsync(string codigo, Guid? exceptoId, CancellationToken ct = default)
    {
        var c = codigo.Trim().ToUpper();
        return _db.Artigos.AnyAsync(a => a.Id != exceptoId
            && (a.CodigoBarras == c || a.CodigoFabricante == c || a.Sku.ToUpper() == c), ct);
    }

    public Task AddAsync(Artigo artigo, CancellationToken ct = default) => _db.Artigos.AddAsync(artigo, ct).AsTask();

    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var novos = _db.ChangeTracker.Entries<Artigo>()
                .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
            var codigoInterno = novos.Where(a => string.IsNullOrEmpty(a.CodigoBarras)).ToHashSet();
            if (novos.Count > 0)
            {
                var tenantId = novos[0].TenantId;
                var max = await _db.Artigos.IgnoreQueryFilters().Where(a => a.TenantId == tenantId)
                    .Select(a => (int?)a.Numero).MaxAsync(ct) ?? 0;
                foreach (var a in novos)
                {
                    a.Numero = ++max;
                    a.Sku = $"LT-{a.Numero:D5}";
                    if (codigoInterno.Contains(a)) a.CodigoBarras = Ean13.Interno(a.Numero);
                }
            }
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && novos.Count > 0)
            {
                foreach (var a in novos)
                {
                    a.Numero = 0;
                    if (codigoInterno.Contains(a)) a.CodigoBarras = string.Empty;
                }
            }
        }
    }
}
