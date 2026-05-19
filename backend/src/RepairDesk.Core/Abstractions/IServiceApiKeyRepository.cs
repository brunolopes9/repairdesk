using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IServiceApiKeyRepository
{
    /// <summary>Lookup por hash — usado pelo auth handler em cada request. Devolve null se revogada.</summary>
    Task<ServiceApiKey?> FindActiveByHashAsync(string keyHash, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceApiKey>> ListByTenantAsync(CancellationToken ct = default);
    Task<ServiceApiKey?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ServiceApiKey key, CancellationToken ct = default);
    /// <summary>Atualização leve do LastUsedAt — chamado em cada request bem-sucedido.</summary>
    Task UpdateLastUsedAsync(Guid id, DateTime utc, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
