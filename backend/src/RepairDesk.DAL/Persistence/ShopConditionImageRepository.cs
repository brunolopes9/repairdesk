using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 531: imagens por estado de condição. O filtro global de tenant + a atribuição de
/// TenantId no SaveChanges são tratados pelo AppDbContext (ITenantEntity).</summary>
public sealed class ShopConditionImageRepository : IShopConditionImageRepository
{
    private readonly AppDbContext _db;

    public ShopConditionImageRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShopConditionImage>> ListAsync(CancellationToken ct = default)
        => await _db.ShopConditionImages.AsNoTracking().ToListAsync(ct);

    public Task<ShopConditionImage?> FindByGradeAsync(string grade, CancellationToken ct = default)
        => _db.ShopConditionImages.FirstOrDefaultAsync(x => x.Grade == grade, ct);

    public async Task AddAsync(ShopConditionImage entity, CancellationToken ct = default)
        => await _db.ShopConditionImages.AddAsync(entity, ct);

    public void Remove(ShopConditionImage entity) => _db.ShopConditionImages.Remove(entity);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
