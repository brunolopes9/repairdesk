using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 531: acesso às imagens por estado de condição (loja online), auto-filtradas por tenant.</summary>
public interface IShopConditionImageRepository
{
    Task<IReadOnlyList<ShopConditionImage>> ListAsync(CancellationToken ct = default);
    /// <summary>Devolve a linha (TRACKED, para upsert) do grau, ou null.</summary>
    Task<ShopConditionImage?> FindByGradeAsync(string grade, CancellationToken ct = default);
    Task AddAsync(ShopConditionImage entity, CancellationToken ct = default);
    void Remove(ShopConditionImage entity);
    Task SaveAsync(CancellationToken ct = default);
}
