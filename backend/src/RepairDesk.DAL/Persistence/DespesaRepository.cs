using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class DespesaRepository : IDespesaRepository
{
    private readonly AppDbContext _db;
    public DespesaRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Despesa>> ListPeriodoAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await _db.Despesas.AsNoTracking().Where(d => !d.IsCogs && d.Data >= from && d.Data < to)
            .OrderBy(d => d.Data).ToListAsync(ct);

    public Task<Despesa?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Despesas.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        bool? isRecorrente, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Filtrar(query, categoria, categoriaIn, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.Data)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<DespesaResumoCategoria>> ResumoAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        bool? isRecorrente, CancellationToken ct = default)
        => await Filtrar(query, categoria, categoriaIn, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente)
            .GroupBy(d => d.Categoria)
            .Select(g => new DespesaResumoCategoria(g.Key, g.Count(), g.Sum(d => d.ValorCents),
                g.Sum(d => d.IvaCents ?? 0), g.Count(d => d.IvaCents == null && !d.ReverseCharge), g.Count(d => d.IsRecorrente)))
            .ToListAsync(ct);

    private IQueryable<Despesa> Filtrar(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to, bool? isRecorrente)
    {
        var q = _db.Despesas.AsNoTracking().AsQueryable();
        var supplierImportDespesaIds = _db.SupplierInvoiceImports
            .Where(i => i.DespesaId != null)
            .Select(i => i.DespesaId!.Value);

        if (categoria is not null) q = q.Where(d => d.Categoria == categoria.Value);
        if (categoriaIn is { Count: > 0 })
        {
            var categorias = categoriaIn.ToArray();
            q = includeSupplierInvoiceImports
                ? q.Where(d => categorias.Contains(d.Categoria) || supplierImportDespesaIds.Contains(d.Id))
                : q.Where(d => categorias.Contains(d.Categoria));
        }
        else if (includeSupplierInvoiceImports)
        {
            q = q.Where(d => supplierImportDespesaIds.Contains(d.Id));
        }
        if (excludeSupplierInvoiceImports) q = q.Where(d => !supplierImportDespesaIds.Contains(d.Id));
        if (from is not null) q = q.Where(d => d.Data >= from.Value);
        if (to is not null) q = q.Where(d => d.Data <= to.Value);
        if (isRecorrente is not null) q = q.Where(d => d.IsRecorrente == isRecorrente.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(d =>
                EF.Functions.Like(d.Descricao, like) ||
                (d.Fornecedor != null && EF.Functions.Like(d.Fornecedor, like)));
        }
        return q;
    }

    public Task AddAsync(Despesa despesa, CancellationToken ct = default) => _db.Despesas.AddAsync(despesa, ct).AsTask();
    public void Remove(Despesa despesa) => _db.Despesas.Remove(despesa);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
