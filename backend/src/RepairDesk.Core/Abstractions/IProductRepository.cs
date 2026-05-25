using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IProductRepository
{
    Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(
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
        CancellationToken ct = default);
    Task<Product?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Product?> FindBySlugAsync(string slug, CancellationToken ct = default);
    /// <summary>Sprint 153: lookup idempotente para CSV importer (Molano etc).</summary>
    Task<Product?> FindByDropshipAsync(Guid fornecedorId, string supplierSku, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Product p, CancellationToken ct = default);
    void Remove(Product p);
    Task SaveAsync(CancellationToken ct = default);
}
