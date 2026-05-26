using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ProductModelRepository : IProductModelRepository
{
    private readonly AppDbContext _db;
    public ProductModelRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductModel>> ListAsync(CancellationToken ct = default) =>
        await _db.ProductModels
            .Include(m => m.Images)
            .OrderBy(m => m.Brand).ThenBy(m => m.Model)
            .ToListAsync(ct);

    public Task<ProductModel?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ProductModels.Include(m => m.Images).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<ProductModel?> FindByBrandModelAsync(string brand, string model, CancellationToken ct = default)
    {
        var b = brand.Trim().ToLower();
        var m = model.Trim().ToLower();
        return _db.ProductModels.Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Brand.ToLower() == b && x.Model.ToLower() == m, ct);
    }

    public async Task AddAsync(ProductModel model, CancellationToken ct = default)
    {
        await _db.ProductModels.AddAsync(model, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(ProductModel model, CancellationToken ct = default)
    {
        _db.ProductModels.Remove(model);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> CountUnitsAsync(Guid modelId, CancellationToken ct = default) =>
        _db.Products.CountAsync(p => p.ModelId == modelId, ct);
}
