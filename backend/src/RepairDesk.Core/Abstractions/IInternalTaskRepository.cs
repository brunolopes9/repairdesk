using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 422: persistência de tarefas internas.</summary>
public interface IInternalTaskRepository
{
    /// <summary>Sprint 617: tarefas pendentes em «Hoje» de um utilizador (criadas por ele ou atribuídas a ele).</summary>
    Task<int> ContarHojePendentesAsync(Guid userId, Guid? exceptoId, CancellationToken ct = default);
    Task<IReadOnlyList<InternalTask>> ListAsync(
        InternalTaskStatus? status,
        Guid? assignedToUserId,
        Guid? vendaId,
        CancellationToken ct = default);

    Task<InternalTask?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(InternalTask task, CancellationToken ct = default);
    void Remove(InternalTask task);
    Task SaveAsync(CancellationToken ct = default);
}
