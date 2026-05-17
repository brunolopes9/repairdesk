using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IClienteRepository
{
    Task<Cliente?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> NifExistsAsync(string nif, Guid? exceptId = null, CancellationToken ct = default);
    Task<Cliente?> FindByNifAsync(string nif, CancellationToken ct = default);
    Task<Cliente?> FindByTelefoneAsync(string telefoneNormalizado, CancellationToken ct = default);
    Task<(IReadOnlyList<Cliente> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> ExportAllAsync(CancellationToken ct = default);
    Task AddAsync(Cliente cliente, CancellationToken ct = default);
    void Remove(Cliente cliente);
    Task SaveAsync(CancellationToken ct = default);
}
