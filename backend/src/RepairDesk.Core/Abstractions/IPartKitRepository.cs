using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IPartKitRepository
{
    Task<IReadOnlyList<PartKit>> ListAsync(CancellationToken ct = default);
    Task<PartKit?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<PartKit?> FindByNomeAsync(string nome, CancellationToken ct = default);
    Task AddAsync(PartKit kit, CancellationToken ct = default);
    Task UpdateAsync(PartKit kit, CancellationToken ct = default);
    Task DeleteAsync(PartKit kit, CancellationToken ct = default);
}
