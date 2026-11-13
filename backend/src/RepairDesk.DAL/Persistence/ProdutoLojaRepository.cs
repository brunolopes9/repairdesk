using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ProdutoLojaRepository : IProdutoLojaRepository
{
    private readonly AppDbContext _db;
    public ProdutoLojaRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProdutoLoja>> ListAsync(bool soPublicados, CancellationToken ct = default)
    {
        var q = _db.ProdutosLoja.Include(p => p.Fotos).AsQueryable();
        if (soPublicados) q = q.Where(p => p.Publicado);
        return await q.OrderByDescending(p => p.Numero).ToListAsync(ct);
    }

    public Task<ProdutoLoja?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.ProdutosLoja.Include(p => p.Fotos).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> SlugExisteAsync(string slug, Guid? exceptoId, CancellationToken ct = default)
        => _db.ProdutosLoja.AnyAsync(p => p.Slug == slug && p.Id != exceptoId, ct);

    public Task<string?> ProdutoDoArtigoAsync(Guid artigoId, Guid? exceptoId, CancellationToken ct = default)
        => _db.ProdutosLoja.Where(p => p.ArtigoId == artigoId && p.Id != exceptoId).Select(p => p.Titulo).FirstOrDefaultAsync(ct);

    public async Task AddAsync(ProdutoLoja produto, CancellationToken ct = default) => await _db.ProdutosLoja.AddAsync(produto, ct);
    public async Task AddFotoAsync(ProdutoLojaFoto foto, CancellationToken ct = default) => await _db.ProdutosLojaFotos.AddAsync(foto, ct);
    public void Remove(ProdutoLojaFoto foto) => _db.ProdutosLojaFotos.Remove(foto);
    public void Remove(ProdutoLoja produto) => _db.ProdutosLoja.Remove(produto);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var novos = _db.ChangeTracker.Entries<ProdutoLoja>()
                .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
            foreach (var p in novos.Where(p => p.TenantId == Guid.Empty && _db.CurrentTenantId is not null)) p.TenantId = _db.CurrentTenantId!.Value;
            foreach (var grupo in novos.GroupBy(p => p.TenantId))
            {
                var max = await _db.ProdutosLoja.IgnoreQueryFilters().Where(p => p.TenantId == grupo.Key)
                    .Select(p => (int?)p.Numero).MaxAsync(ct) ?? 0;
                foreach (var p in grupo) p.Numero = ++max;
            }
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && novos.Count > 0)
            {
                foreach (var p in novos) p.Numero = 0;
            }
        }
    }
}
