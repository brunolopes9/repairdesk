using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class SkuMappingRepository : ISkuMappingRepository
{
    private readonly AppDbContext _db;

    public SkuMappingRepository(AppDbContext db) => _db = db;

    public Task<SkuMapping?> FindAsync(Guid tenantId, string supplierCode, string supplierSku, CancellationToken ct = default)
    {
        var normSupplier = supplierCode.Trim().ToLowerInvariant();
        var normSku = supplierSku.Trim();
        return _db.SkuMappings
            .Where(m => m.TenantId == tenantId
                && m.SupplierCode == normSupplier
                && m.SupplierSku == normSku)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SkuMapping>> ListBySupplierAsync(Guid tenantId, string supplierCode, CancellationToken ct = default)
    {
        var normSupplier = supplierCode.Trim().ToLowerInvariant();
        return await _db.SkuMappings
            .Where(m => m.TenantId == tenantId && m.SupplierCode == normSupplier)
            .OrderByDescending(m => m.UseCount)
            .ToListAsync(ct);
    }

    public Task AddAsync(SkuMapping mapping, CancellationToken ct = default)
        => _db.SkuMappings.AddAsync(mapping, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
