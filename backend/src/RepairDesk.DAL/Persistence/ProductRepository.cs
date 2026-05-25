using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(
        string? search,
        string? brand,
        bool? lojaOnline,
        Guid? fornecedorId,
        bool? ativo,
        bool? mostrarLojaOnline,
        string? sort,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.Products.AsNoTracking().AsQueryable();
        if (ativo.HasValue) q = q.Where(p => p.Active == ativo.Value);
        else if (!includeInactive) q = q.Where(p => p.Active);
        var onlineFilter = mostrarLojaOnline ?? lojaOnline;
        if (onlineFilter.HasValue) q = q.Where(p => p.MostrarLojaOnline == onlineFilter.Value);
        if (fornecedorId.HasValue)
        {
            q = fornecedorId.Value == Guid.Empty
                ? q.Where(p => p.FornecedorId == null)
                : q.Where(p => p.FornecedorId == fornecedorId.Value);
        }
        if (!string.IsNullOrWhiteSpace(brand)) q = q.Where(p => p.Brand == brand);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(p =>
                p.Sku.Contains(term)
                || p.Brand.Contains(term)
                || p.Model.Contains(term)
                || (p.Storage != null && p.Storage.Contains(term))
                || (p.Color != null && p.Color.Contains(term)));
        }
        var total = await q.CountAsync(ct);
        var ordered = (sort?.Trim().ToLowerInvariant()) switch
        {
            "brand" => q.OrderBy(p => p.Brand).ThenBy(p => p.Model),
            "-brand" => q.OrderByDescending(p => p.Brand).ThenByDescending(p => p.Model),
            "price" => q.OrderBy(p => p.PriceCents),
            "-price" => q.OrderByDescending(p => p.PriceCents),
            "created" => q.OrderByDescending(p => p.CreatedAt),
            "updated" or "-updated" or null or "" => q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt),
            _ => q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt),
        };
        var items = await ordered
            .Include(p => p.Images)
            .Include(p => p.Fornecedor)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Products.Include(p => p.Images).Include(p => p.Fornecedor).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> FindBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<Product?> FindByDropshipAsync(Guid fornecedorId, string supplierSku, CancellationToken ct = default)
        => _db.Products
            .Include(p => p.Images)
            .Include(p => p.Fornecedor)
            .FirstOrDefaultAsync(p => p.FornecedorId == fornecedorId && p.DropshipSupplierSku == supplierSku, ct);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Sku == sku && (excludeId == null || p.Id != excludeId), ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Slug == slug && (excludeId == null || p.Id != excludeId), ct);

    public Task AddAsync(Product p, CancellationToken ct = default)
        => _db.Products.AddAsync(p, ct).AsTask();

    public void Remove(Product p) => _db.Products.Remove(p);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
