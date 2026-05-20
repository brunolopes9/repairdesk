using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IFornecedorRepository
{
    Task<IReadOnlyList<Fornecedor>> ListByTenantAsync(bool includeInactive, CancellationToken ct = default);
    Task<Fornecedor?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Fornecedor?> FindByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Fornecedor f, CancellationToken ct = default);
    void Remove(Fornecedor f);
    Task SaveAsync(CancellationToken ct = default);
}
