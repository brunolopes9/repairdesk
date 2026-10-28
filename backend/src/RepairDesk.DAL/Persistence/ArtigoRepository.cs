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
        .Include(a => a.Codigos)
        .Include(a => a.Lotes).ThenInclude(l => l.Documento).ThenInclude(d => d!.Fornecedor);

    public async Task<IReadOnlyList<Artigo>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default)
    {
        var query = ComLotes().AsNoTracking().Where(a => incluirInativos || a.Ativo);
        foreach (var termo in PesquisaTexto.Termos(q))
        {
            var t = termo.ToUpper();
            query = query.Where(a => a.Nome.ToUpper().Contains(t) || a.Sku.ToUpper().Contains(t) || a.CodigoBarras.Contains(t)
                || (a.CodigoFabricante != null && a.CodigoFabricante.Contains(t)) || (a.Categoria != null && a.Categoria.ToUpper().Contains(t))
                // Sprint 581: e pelos modelos dos lotes em stock (artigos agrupados: "iphone 11" → "Película vidro").
                || a.Lotes.Any(l => l.Quantidade - l.QuantidadeVendida - l.QuantidadeAbatida > 0 && l.Descricao.ToUpper().Contains(t)));
        }
        return await query.OrderBy(a => a.Nome).ToListAsync(ct);
    }

    public Task<Artigo?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => ComLotes().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Artigo?> FindByCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var c = codigo.Trim().ToUpper();
        // Sprint 581: também os códigos alternativos (etiquetas de artigos que foram juntos neste).
        return ComLotes().Where(a => a.CodigoBarras == c || a.CodigoFabricante == c || a.Sku.ToUpper() == c
                || a.Codigos.Any(x => x.FornecedorId == null && x.Codigo == c))
            .OrderByDescending(a => a.Ativo).FirstOrDefaultAsync(ct);
    }

    public Task<bool> CodigoEmUsoAsync(string codigo, Guid? exceptoId, CancellationToken ct = default)
    {
        var c = codigo.Trim().ToUpper();
        return _db.Artigos.AnyAsync(a => a.Id != exceptoId
            && (a.CodigoBarras == c || a.CodigoFabricante == c || a.Sku.ToUpper() == c
                || a.Codigos.Any(x => x.FornecedorId == null && x.Codigo == c)), ct);
    }

    public void Remove(Artigo artigo) => _db.Artigos.Remove(artigo);

    public async Task<Guid?> ArtigoPorSkuFornecedorAsync(Guid fornecedorId, string sku, CancellationToken ct = default)
    {
        var c = sku.Trim().ToUpper();
        return await _db.ArtigoCodigos.Where(x => x.FornecedorId == fornecedorId && x.Codigo == c && x.Artigo!.Ativo)
            .Select(x => (Guid?)x.ArtigoId).FirstOrDefaultAsync(ct);
    }

    public async Task AprenderSkuFornecedorAsync(Guid artigoId, Guid fornecedorId, string sku, CancellationToken ct = default)
    {
        var c = sku.Trim().ToUpper();
        if (c.Length == 0) return;
        if (c.Length > 64) c = c[..64];
        var existente = await _db.ArtigoCodigos.FirstOrDefaultAsync(x => x.FornecedorId == fornecedorId && x.Codigo == c, ct)
            ?? _db.ArtigoCodigos.Local.FirstOrDefault(x => x.FornecedorId == fornecedorId && x.Codigo == c && _db.Entry(x).State == EntityState.Added);
        if (existente is null)
            await _db.ArtigoCodigos.AddAsync(new ArtigoCodigo { ArtigoId = artigoId, FornecedorId = fornecedorId, Codigo = c }, ct);
        else existente.ArtigoId = artigoId;
    }

    public Task AddCodigoAsync(ArtigoCodigo codigo, CancellationToken ct = default) => _db.ArtigoCodigos.AddAsync(codigo, ct).AsTask();

    public Task<bool> SkuApontaParaOutroAsync(Guid artigoId, Guid fornecedorId, string sku, CancellationToken ct = default)
    {
        var c = sku.Trim().ToUpper();
        return _db.ArtigoCodigos.AnyAsync(x => x.FornecedorId == fornecedorId && x.Codigo == c && x.ArtigoId != artigoId, ct);
    }

    public Task AddFusaoAsync(ArtigoFusao fusao, CancellationToken ct = default) => _db.ArtigoFusoes.AddAsync(fusao, ct).AsTask();

    public Task<ArtigoFusao?> FindFusaoAsync(Guid id, CancellationToken ct = default)
        => _db.ArtigoFusoes.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<ArtigoFusao>> ListFusoesAsync(Guid destinoId, CancellationToken ct = default)
        => await _db.ArtigoFusoes.AsNoTracking().Where(f => f.DestinoId == destinoId && f.DesfeitaEm == null)
            .OrderByDescending(f => f.CreatedAt).ToListAsync(ct);

    public async Task<Artigo?> FindApagadoAsync(Guid id, CancellationToken ct = default)
    {
        // IgnoreQueryFilters tira também o filtro de tenant: confirma-o à mão com o de um artigo visível do contexto.
        var a = await _db.Artigos.IgnoreQueryFilters().Include(x => x.Codigos).FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is not null && await _db.ArtigoFusoes.AnyAsync(f => f.OrigemId == id, ct) ? a : null;
    }

    public async Task<IReadOnlyList<ArtigoCodigo>> FindCodigosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await _db.ArtigoCodigos.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToListAsync(ct);

    public void RemoveCodigo(ArtigoCodigo codigo) => _db.ArtigoCodigos.Remove(codigo);

    public Task SaveIntermedioAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task AddAsync(Artigo artigo, CancellationToken ct = default) => _db.Artigos.AddAsync(artigo, ct).AsTask();

    public async Task<IReadOnlyList<CategoriaArtigo>> ListCategoriasAsync(CancellationToken ct = default)
        => await _db.CategoriasArtigo.OrderBy(c => c.Nome).ToListAsync(ct);

    public Task<CategoriaArtigo?> FindCategoriaAsync(string nome, CancellationToken ct = default)
    {
        var n = nome.Trim().ToUpper();
        return _db.CategoriasArtigo.FirstOrDefaultAsync(c => c.Nome.ToUpper() == n, ct);
    }

    public Task AddCategoriaAsync(CategoriaArtigo categoria, CancellationToken ct = default)
        => _db.CategoriasArtigo.AddAsync(categoria, ct).AsTask();

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
