using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface ITrabalhoRepository
{
    Task<Trabalho?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task CreateWithNextNumeroAsync(Trabalho trabalho, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Trabalho> Items, int Total)> SearchAsync(
        string? query,
        TrabalhoStatus? status,
        JobCategory? categoria,
        Guid? clienteId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    void Remove(Trabalho trabalho);
    Task SaveAsync(CancellationToken ct = default);
}
