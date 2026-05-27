using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 385 (Doc 87): leitura agregada para a página unificada "Catálogo &amp; Stock".
/// Junta as três fontes — <see cref="ProductModel"/> (pai retail), <see cref="Product"/>
/// (variante retail) e <see cref="Part"/> (stock técnico) — numa só carga. É só LEITURA;
/// as escritas continuam nos serviços/repos próprios de cada entidade (sem fusão de BD).
/// </summary>
public interface ICatalogReadRepository
{
    Task<CatalogReadData> LoadAsync(CancellationToken ct = default);
}

/// <summary>Snapshot das três fontes do catálogo (filtragem/agrupamento faz-se no serviço).</summary>
public sealed record CatalogReadData(
    IReadOnlyList<ProductModel> Models,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Part> Parts);
