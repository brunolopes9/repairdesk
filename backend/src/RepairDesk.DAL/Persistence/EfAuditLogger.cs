using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class EfAuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly ILogger<EfAuditLogger> _logger;

    public EfAuditLogger(AppDbContext db, ITenantContext tenant, ICurrentUser user, ILogger<EfAuditLogger> logger)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _logger = logger;
    }

    public async Task LogAsync(
        AuditAction action,
        string entityType,
        Guid? entityId,
        object? changes = null,
        Guid? tenantId = null,
        Guid? appUserId = null,
        CancellationToken ct = default)
    {
        try
        {
            var tid = tenantId ?? _tenant.TenantId;
            if (tid is null) return;

            var resolvedAppUserId = appUserId ?? _user.UserId;
            _db.AuditEntries.Add(new AuditEntry
            {
                TenantId = tid.Value,
                AppUserId = resolvedAppUserId,
                // Quando explicitamente injectado um appUserId (operações batch),
                // assume-se contexto de utilizador real e não se carrega ServiceApiKeyId.
                ServiceApiKeyId = appUserId is null ? _user.ServiceApiKeyId : null,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes),
                IpAddress = _user.IpAddress,
                UserAgent = _user.UserAgent,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit entry for {EntityType}/{EntityId}", entityType, entityId);
        }
    }
}
