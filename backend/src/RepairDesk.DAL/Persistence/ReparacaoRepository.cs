using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class ReparacaoRepository : IReparacaoRepository
{
    private readonly AppDbContext _db;

    public ReparacaoRepository(AppDbContext db) => _db = db;

    public Task<Reparacao?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Reparacoes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Reparacao?> FindByIdWithTimelineAsync(Guid id, CancellationToken ct = default)
        => _db.Reparacoes
            .Include(r => r.Cliente)
            .Include(r => r.Timeline.OrderBy(t => t.MudouEm))
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Reparacao?> FindByPublicSlugWithTimelineAsync(string slug, CancellationToken ct = default)
        => _db.Reparacoes
            .IgnoreQueryFilters() // endpoint público — sem filtro de tenant
            .Include(r => r.Cliente)
            .Include(r => r.Timeline.OrderBy(t => t.MudouEm))
            .Where(r => !r.IsDeleted)
            .FirstOrDefaultAsync(r => r.PublicSlug == slug, ct);

    public async Task CreateWithNextNumeroAsync(Reparacao reparacao, Guid tenantId, CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var max = await _db.Reparacoes
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .Select(r => (int?)r.Numero)
                .MaxAsync(ct);
            reparacao.Numero = (max ?? 0) + 1;
            _db.Reparacoes.Add(reparacao);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                _db.Entry(reparacao).State = EntityState.Detached;
                foreach (var log in reparacao.Timeline)
                    _db.Entry(log).State = EntityState.Detached;
            }
        }
    }

    public async Task<(IReadOnlyList<Reparacao> Items, int Total)> SearchAsync(
        string? query, RepairStatus? estado, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Reparacoes.AsNoTracking().Include(r => r.Cliente).AsQueryable();

        if (estado is not null) q = q.Where(r => r.Estado == estado.Value);
        if (clienteId is not null) q = q.Where(r => r.ClienteId == clienteId.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(r =>
                EF.Functions.Like(r.Equipamento, like) ||
                EF.Functions.Like(r.Avaria, like) ||
                (r.Imei != null && EF.Functions.Like(r.Imei, like)) ||
                (r.Cliente != null && (
                    EF.Functions.Like(r.Cliente.Nome, like) ||
                    EF.Functions.Like(r.Cliente.Telefone, like))));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<Reparacao>> ExportAllAsync(CancellationToken ct = default)
        => await _db.Reparacoes
            .AsNoTracking()
            .Include(r => r.Cliente)
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _db.Reparacoes.AsNoTracking().AnyAsync(ct);

    public async Task<IReadOnlyList<Reparacao>> SearchByImeiAsync(string imeiNormalizado, Guid? excludeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imeiNormalizado))
            return Array.Empty<Reparacao>();

        var q = _db.Reparacoes
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Where(r => r.Imei != null && r.Imei == imeiNormalizado);
        if (excludeId is not null) q = q.Where(r => r.Id != excludeId.Value);
        return await q
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    public void Remove(Reparacao reparacao) => _db.Reparacoes.Remove(reparacao);
    public void AddEstadoLog(ReparacaoEstadoLog log) => _db.ReparacaoEstadoLogs.Add(log);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
