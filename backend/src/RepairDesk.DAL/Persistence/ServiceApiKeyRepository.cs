using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ServiceApiKeyRepository : IServiceApiKeyRepository
{
    private readonly AppDbContext _db;
    public ServiceApiKeyRepository(AppDbContext db) => _db = db;

    public Task<ServiceApiKey?> FindActiveByHashAsync(string keyHash, CancellationToken ct = default)
        => _db.ServiceApiKeys
            .IgnoreQueryFilters()  // auth runs antes do TenantContext estar populado
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.RevokedAt == null && !k.IsDeleted, ct);

    public async Task<IReadOnlyList<ServiceApiKey>> ListByTenantAsync(CancellationToken ct = default)
        => await _db.ServiceApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public Task<ServiceApiKey?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ServiceApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

    public Task AddAsync(ServiceApiKey key, CancellationToken ct = default)
        => _db.ServiceApiKeys.AddAsync(key, ct).AsTask();

    public async Task UpdateLastUsedAsync(Guid id, DateTime utc, CancellationToken ct = default)
    {
        // Update direto via ExecuteUpdate — não precisa de tracking nem trigger do SaveChanges.
        await _db.ServiceApiKeys
            .IgnoreQueryFilters()
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, utc), ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
