using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IProductModelRepository
{
    Task<IReadOnlyList<ProductModel>> ListAsync(CancellationToken ct = default);
    Task<ProductModel?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Lookup pela chave de negócio (Brand+Model) — usado pelo importer para reutilizar template.</summary>
    Task<ProductModel?> FindByBrandModelAsync(string brand, string model, CancellationToken ct = default);
    Task AddAsync(ProductModel model, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task DeleteAsync(ProductModel model, CancellationToken ct = default);
    /// <summary>Nº de unidades (Product) ligadas a este modelo — para impedir delete com variantes.</summary>
    Task<int> CountUnitsAsync(Guid modelId, CancellationToken ct = default);
}
