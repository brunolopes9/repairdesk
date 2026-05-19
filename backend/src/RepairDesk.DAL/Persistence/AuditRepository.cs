using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditEntry> Items, int Total)> SearchAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = BaseQuery(query.IncludeAllTenants);

        if (query.EntityTypes.Count > 0)
            q = q.Where(a => query.EntityTypes.Contains(a.EntityType));
        if (query.EntityId is not null)
            q = q.Where(a => a.EntityId == query.EntityId);
        if (query.UserIds.Count > 0)
            q = q.Where(a => a.AppUserId != null && query.UserIds.Contains(a.AppUserId.Value));
        if (query.ServiceApiKeyIds.Count > 0)
            q = q.Where(a => a.ServiceApiKeyId != null && query.ServiceApiKeyIds.Contains(a.ServiceApiKeyId.Value));
        if (query.Actions.Count > 0)
            q = q.Where(a => query.Actions.Contains(a.Action));
        if (query.From is not null)
            q = q.Where(a => a.CreatedAt >= query.From.Value);
        if (query.To is not null)
            q = q.Where(a => a.CreatedAt < query.To.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(a =>
                a.EntityType.Contains(term)
                || (a.ChangesJson != null && a.ChangesJson.Contains(term))
                || (a.IpAddress != null && a.IpAddress.Contains(term))
                || (a.UserAgent != null && a.UserAgent.Contains(term))
                || (a.AppUser != null && (a.AppUser.DisplayName.Contains(term) || (a.AppUser.Email != null && a.AppUser.Email.Contains(term)))));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<AuditFilterOptionsSnapshot> GetFilterOptionsAsync(bool includeAllTenants, CancellationToken ct = default)
    {
        var q = BaseQuery(includeAllTenants);
        var entityTypes = await q
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
        var actions = await q
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
        // Sprint 66: query separada para users — distinct em record com 3 fields
        // dava 500 quando AppUser.DisplayName variava entre AuditEntries com mesmo UserId.
        // Agrupar por UserId e ir buscar o user uma vez é mais robusto.
        var userIds = await q
            .Where(a => a.AppUserId != null)
            .Select(a => a.AppUserId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var users = new List<AuditUserOptionRow>();
        if (userIds.Count > 0)
        {
            users = await _db.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new AuditUserOptionRow(u.Id, u.DisplayName, u.Email))
                .OrderBy(u => u.DisplayName)
                .ToListAsync(ct);
            // IDs órfãos (user apagado mas audit log preservado) — fallback para GUID curto.
            var fetched = users.Select(u => u.Id).ToHashSet();
            foreach (var id in userIds.Where(id => !fetched.Contains(id)))
                users.Add(new AuditUserOptionRow(id, $"Utilizador removido ({id.ToString()[..8]})", null));
        }

        // Sprint 100: lista de API keys que aparecem no audit (ainda activas OU revogadas).
        var apiKeyIds = await q
            .Where(a => a.ServiceApiKeyId != null)
            .Select(a => a.ServiceApiKeyId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var apiKeys = new List<AuditApiKeyOptionRow>();
        if (apiKeyIds.Count > 0)
        {
            var keysQuery = includeAllTenants ? _db.ServiceApiKeys.IgnoreQueryFilters() : _db.ServiceApiKeys;
            apiKeys = await keysQuery
                .AsNoTracking()
                .Where(k => apiKeyIds.Contains(k.Id))
                .Select(k => new AuditApiKeyOptionRow(k.Id, k.Name, k.KeyPrefix, k.RevokedAt != null))
                .OrderBy(k => k.Name)
                .ToListAsync(ct);
            var fetched = apiKeys.Select(k => k.Id).ToHashSet();
            foreach (var id in apiKeyIds.Where(id => !fetched.Contains(id)))
                apiKeys.Add(new AuditApiKeyOptionRow(id, $"Chave removida ({id.ToString()[..8]})", "", true));
        }

        return new AuditFilterOptionsSnapshot(entityTypes, users, actions, apiKeys);
    }

    private IQueryable<AuditEntry> BaseQuery(bool includeAllTenants)
    {
        return includeAllTenants
            ? _db.AuditEntries.IgnoreQueryFilters().AsNoTracking().Include(a => a.AppUser).Include(a => a.ServiceApiKey)
            : _db.AuditEntries.AsNoTracking().Include(a => a.AppUser).Include(a => a.ServiceApiKey);
    }
}
