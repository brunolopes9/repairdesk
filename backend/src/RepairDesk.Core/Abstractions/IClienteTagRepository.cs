using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 480: persistence for customer segment tags.</summary>
public interface IClienteTagRepository
{
    Task<IReadOnlyList<ClienteTag>> ListAsync(CancellationToken ct = default);
    Task<ClienteTag?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClienteTag?> FindByNomeAsync(string nome, CancellationToken ct = default);
    Task AddAsync(ClienteTag tag, CancellationToken ct = default);
    Task UpdateAsync(ClienteTag tag, CancellationToken ct = default);
    Task DeleteAsync(ClienteTag tag, CancellationToken ct = default);
    Task SetTagsForClienteAsync(Guid clienteId, IReadOnlyList<Guid> tagIds, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteTag>> ListByClienteAsync(Guid clienteId, CancellationToken ct = default);
}
