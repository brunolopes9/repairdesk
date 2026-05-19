using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class VendaRepository : IVendaRepository
{
    private readonly AppDbContext _db;

    public VendaRepository(AppDbContext db) => _db = db;

    public Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Vendas
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default)
        => _db.Vendas
            .Include(v => v.Cliente)
            .Include(v => v.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task CreateWithNextNumeroAsync(Venda venda, Guid tenantId, CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var max = await _db.Vendas
                .IgnoreQueryFilters()
                .Where(v => v.TenantId == tenantId)
                .Select(v => (int?)v.Numero)
                .MaxAsync(ct);

            venda.Numero = (max ?? 0) + 1;
            _db.Vendas.Add(venda);

            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                _db.Entry(venda).State = EntityState.Detached;
                foreach (var item in venda.Items)
                    _db.Entry(item).State = EntityState.Detached;
            }
        }
    }

    public async Task<(IReadOnlyList<Venda> Items, int Total)> SearchAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? clienteId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.Vendas
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Items)
            .AsQueryable();

        if (fromUtc is not null) q = q.Where(v => v.Data >= fromUtc.Value);
        if (toUtc is not null) q = q.Where(v => v.Data < toUtc.Value);
        if (clienteId is { } cid) q = q.Where(v => v.ClienteId == cid);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(v => v.Data)
            .ThenByDescending(v => v.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> SumPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => await _db.Vendas
            .Where(v => v.Status == VendaStatus.Paga && v.Data >= fromUtc && v.Data < toUtc)
            .SumAsync(v => v.TotalCents, ct);

    public async Task<IReadOnlyList<TopVendaItemRow>> TopItemsByRevenueAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
        => await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Venda != null
                        && i.Venda.Status == VendaStatus.Paga
                        && i.Venda.Data >= fromUtc
                        && i.Venda.Data < toUtc)
            .GroupBy(i => new { i.PartId, i.Descricao })
            .Select(g => new TopVendaItemRow(
                g.Key.PartId,
                g.Key.Descricao,
                g.Sum(x => x.Quantidade),
                g.Sum(x => Math.Max(0, x.Quantidade * x.PrecoUnitarioCents - x.DescontoCents))))
            .OrderByDescending(i => i.TotalCents)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<VendaImeiLookupRow?> FindVendaByImeiAsync(string imei, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imei)) return null;

        // Apenas vendas Paga ou Cancelada (Pendente nao deve aparecer no warning de duplicado).
        var hit = await _db.VendaItems
            .AsNoTracking()
            .Include(i => i.Venda)
                .ThenInclude(v => v!.Cliente)
            .Where(i => (i.Imei == imei || i.Imei2 == imei)
                        && i.Venda != null
                        && i.Venda.Status != VendaStatus.Pendente)
            .OrderByDescending(i => i.Venda!.Data)
            .Select(i => new VendaImeiLookupRow(
                i.Venda!.Id,
                i.Venda.Numero,
                i.Venda.Data,
                i.Descricao,
                i.Venda.Cliente != null ? i.Venda.Cliente.Nome : null))
            .FirstOrDefaultAsync(ct);
        return hit;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
