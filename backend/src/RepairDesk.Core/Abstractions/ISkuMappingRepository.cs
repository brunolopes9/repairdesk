using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ISkuMappingRepository
{
    /// <summary>Lookup exacto por (tenant, supplierCode, supplierSku). Case-insensitive.</summary>
    Task<SkuMapping?> FindAsync(Guid tenantId, string supplierCode, string supplierSku, CancellationToken ct = default);

    /// <summary>Mappings existentes para todos os SKUs deste supplier — para batch dedupe.</summary>
    Task<IReadOnlyList<SkuMapping>> ListBySupplierAsync(Guid tenantId, string supplierCode, CancellationToken ct = default);

    Task AddAsync(SkuMapping mapping, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
