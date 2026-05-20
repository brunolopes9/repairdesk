using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Fornecedores;

public interface IFornecedorService
{
    Task<IReadOnlyList<FornecedorDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<FornecedorDto> CreateAsync(FornecedorWriteRequest req, CancellationToken ct = default);
    Task<FornecedorDto> UpdateAsync(Guid id, FornecedorWriteRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed record FornecedorDto(
    Guid Id,
    string Name,
    string? Email,
    string? RmaEmail,
    string? Phone,
    string? Website,
    int? GarantiaB2BDiasDefault,
    string? Notas,
    bool Active,
    DateTime CreatedAt);

public sealed record FornecedorWriteRequest(
    string Name,
    string? Email,
    string? RmaEmail,
    string? Phone,
    string? Website,
    int? GarantiaB2BDiasDefault,
    string? Notas,
    bool Active);

public class FornecedorService : IFornecedorService
{
    private readonly IFornecedorRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public FornecedorService(IFornecedorRepository repo, ITenantContext tenant, IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<FornecedorDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = await _repo.ListByTenantAsync(includeInactive, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<FornecedorDto> CreateAsync(FornecedorWriteRequest req, CancellationToken ct = default)
    {
        var name = Validate(req);
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var existing = await _repo.FindByNameAsync(name, ct);
        if (existing is not null)
            throw new ConflictException("name_already_exists", $"Já existe um fornecedor com o nome '{name}'.");

        var entity = new Fornecedor
        {
            TenantId = tenantId,
            Name = name,
            Email = Clean(req.Email),
            RmaEmail = Clean(req.RmaEmail),
            Phone = Clean(req.Phone),
            Website = Clean(req.Website),
            GarantiaB2BDiasDefault = req.GarantiaB2BDiasDefault is > 0 ? req.GarantiaB2BDiasDefault : null,
            Notas = Clean(req.Notas),
            Active = req.Active,
        };
        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Fornecedor), entity.Id, new { entity.Name }, ct: ct);
        return ToDto(entity);
    }

    public async Task<FornecedorDto> UpdateAsync(Guid id, FornecedorWriteRequest req, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("Fornecedor", id);
        var name = Validate(req);

        // Se o nome mudou, verificar conflito.
        if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _repo.FindByNameAsync(name, ct);
            if (conflict is not null && conflict.Id != entity.Id)
                throw new ConflictException("name_already_exists", $"Já existe um fornecedor com o nome '{name}'.");
        }

        entity.Name = name;
        entity.Email = Clean(req.Email);
        entity.RmaEmail = Clean(req.RmaEmail);
        entity.Phone = Clean(req.Phone);
        entity.Website = Clean(req.Website);
        entity.GarantiaB2BDiasDefault = req.GarantiaB2BDiasDefault is > 0 ? req.GarantiaB2BDiasDefault : null;
        entity.Notas = Clean(req.Notas);
        entity.Active = req.Active;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Fornecedor), entity.Id, new { entity.Name, entity.Active }, ct: ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("Fornecedor", id);
        _repo.Remove(entity);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(Fornecedor), id, new { entity.Name }, ct: ct);
    }

    private static string Validate(FornecedorWriteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("name_required", "Nome obrigatório.");
        var name = req.Name.Trim();
        if (name.Length > 200)
            throw new ValidationException("name_too_long", "Nome até 200 caracteres.");
        if (req.GarantiaB2BDiasDefault is < 0 or > 365 * 5)
            throw new ValidationException("garantia_dias_invalida", "Dias de garantia entre 0 e 1825.");
        return name;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static FornecedorDto ToDto(Fornecedor f) =>
        new(f.Id, f.Name, f.Email, f.RmaEmail, f.Phone, f.Website, f.GarantiaB2BDiasDefault, f.Notas, f.Active, f.CreatedAt);
}
