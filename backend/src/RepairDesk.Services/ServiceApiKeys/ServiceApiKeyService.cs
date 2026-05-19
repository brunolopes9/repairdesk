using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Audit;

namespace RepairDesk.Services.ServiceApiKeys;

public interface IServiceApiKeyService
{
    Task<IReadOnlyList<ServiceApiKeyDto>> ListAsync(CancellationToken ct = default);
    Task<CreateServiceApiKeyResponse> CreateAsync(string name, CancellationToken ct = default);
    Task RevokeAsync(Guid id, string? reason, CancellationToken ct = default);
}

public sealed record ServiceApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt,
    string? RevokedReason);

/// <summary>Devolve o plain key UMA VEZ ao criar — depois só existe o hash.</summary>
public sealed record CreateServiceApiKeyResponse(ServiceApiKeyDto Key, string PlainKey);

public sealed record CreateServiceApiKeyRequest(string Name);
public sealed record RevokeServiceApiKeyRequest(string? Reason);

public class ServiceApiKeyService : IServiceApiKeyService
{
    private readonly IServiceApiKeyRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public ServiceApiKeyService(IServiceApiKeyRepository repo, ITenantContext tenant, IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ServiceApiKeyDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _repo.ListByTenantAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<CreateServiceApiKeyResponse> CreateAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name_required", "Nome da chave obrigatório (ex: 'Loja online').");
        name = name.Trim();
        if (name.Length > 200)
            throw new ValidationException("name_too_long", "Nome até 200 caracteres.");

        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var (plainKey, hash, displayPrefix) = ApiKeyGenerator.Generate();

        var entity = new ServiceApiKey
        {
            TenantId = tenantId,
            Name = name,
            KeyPrefix = displayPrefix,
            KeyHash = hash,
        };
        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(
            AuditAction.Create,
            "ServiceApiKey",
            entity.Id,
            new { entity.Name, entity.KeyPrefix },
            tenantId,
            ct: ct);

        return new CreateServiceApiKeyResponse(ToDto(entity), plainKey);
    }

    public async Task RevokeAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        var key = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("ServiceApiKey", id);
        if (key.RevokedAt is not null)
            throw new ConflictException("already_revoked", "Chave já revogada.");

        key.RevokedAt = DateTime.UtcNow;
        key.RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(
            AuditAction.Update,
            "ServiceApiKey",
            key.Id,
            new { action = "revoked", reason = key.RevokedReason, key.KeyPrefix },
            _tenant.TenantId,
            ct: ct);
    }

    private static ServiceApiKeyDto ToDto(ServiceApiKey k) =>
        new(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt, k.RevokedAt, k.RevokedReason);
}
