using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IPartRepository
{
    Task<Part?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Part?> FindByIdWithMovimentosAsync(Guid id, CancellationToken ct = default);
    Task<Part?> FindBySkuAsync(string sku, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, Guid? exceptId = null, CancellationToken ct = default);
    Task<(IReadOnlyList<Part> Items, int Total)> SearchAsync(
        string? query,
        PartCategoria? categoria,
        string? marca,
        bool lowStockOnly,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<Part>> LowStockAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default);
    Task AddAsync(Part part, CancellationToken ct = default);
    void Remove(Part part);
    void AddMovimento(PartMovimento movimento);
    Task<IReadOnlyList<PartMovimento>> MovimentosAsync(Guid? partId, Guid? reparacaoId, CancellationToken ct = default);
    Task<int> SumCustoByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    /// <summary>Sprint 186: previsão reabastecimento — devolve Parts onde consumo30d
    /// excede o stock actual (Bruno vai ficar sem stock antes de fechar o mês).</summary>
    Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoesAsync(int days, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Sprint 186: linha de sugestão reabastecimento.</summary>
public sealed record ReabastecerSugestao(
    Guid PartId,
    string Sku,
    string Nome,
    int QtdStockActual,
    int ConsumoDias,
    int DiasRestantesEstimados,
    int CustoUnitarioCents);
