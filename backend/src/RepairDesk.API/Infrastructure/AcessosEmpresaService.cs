using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Infrastructure;

public sealed record EmpresaDisponivelDto(Guid Id, string Nome, bool Ativa, bool Principal);

/// <summary>
/// Sprint 606: a que empresas um utilizador pode aceder — a sua (<see cref="AppUser.TenantId"/>) e as
/// que tem em <see cref="AcessoEmpresa"/>. Consulta sem filtros de tenant (decide antes de haver um).
/// </summary>
public sealed class AcessosEmpresaService
{
    private readonly AppDbContext _db;
    public AcessosEmpresaService(AppDbContext db) => _db = db;

    public async Task<bool> PodeAsync(AppUser user, Guid tenantId, CancellationToken ct)
        => tenantId == user.TenantId
           || await _db.AcessosEmpresa.AnyAsync(a => a.UserId == user.Id && a.TenantId == tenantId, ct)
              && await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId && t.IsActive, ct);

    public async Task<IReadOnlyList<EmpresaDisponivelDto>> ListarAsync(AppUser user, Guid ativa, CancellationToken ct)
    {
        var extra = await _db.AcessosEmpresa.Where(a => a.UserId == user.Id).Select(a => a.TenantId).ToListAsync(ct);
        var ids = extra.Append(user.TenantId).Distinct().ToList();
        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id) && t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);
        return tenants
            .Select(t => new EmpresaDisponivelDto(t.Id, t.Name, t.Id == ativa, t.Id == user.TenantId))
            .OrderByDescending(e => e.Principal).ThenBy(e => e.Nome)
            .ToList();
    }

    public async Task DarAcessoAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        if (await _db.AcessosEmpresa.AnyAsync(a => a.UserId == userId && a.TenantId == tenantId, ct)) return;
        _db.AcessosEmpresa.Add(new AcessoEmpresa { UserId = userId, TenantId = tenantId });
        await _db.SaveChangesAsync(ct);
    }
}
