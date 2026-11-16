using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IAlertaStockRepository
{
    Task<bool> ExisteAsync(string slug, string email, CancellationToken ct = default);
    Task<int> ContarPorSlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<AlertaStockLoja>> DosSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, int>> ContagensAsync(CancellationToken ct = default);
    Task AddAsync(AlertaStockLoja alerta, CancellationToken ct = default);
    void Remove(AlertaStockLoja alerta);
    Task SaveAsync(CancellationToken ct = default);
}
