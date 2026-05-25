using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class DespesaRepository : IDespesaRepository
{
    private readonly AppDbContext _db;
    public DespesaRepository(AppDbContext db) => _db = db;

    public Task<Despesa?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Despesas.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<int> SumByTrabalhoAsync(Guid trabalhoId, CancellationToken ct = default)
        => await _db.Despesas.Where(d => d.TrabalhoId == trabalhoId).SumAsync(d => (int?)d.ValorCents, ct) ?? 0;

    public async Task<int> SumByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.Despesas.Where(d => d.ReparacaoId == reparacaoId).SumAsync(d => (int?)d.ValorCents, ct) ?? 0;

    public async Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        Guid? trabalhoId, Guid? reparacaoId, bool? isRecorrente, int page, int pageSize, CancellationToken ct = default)
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
        if (trabalhoId is not null) q = q.Where(d => d.TrabalhoId == trabalhoId.Value);
        if (reparacaoId is not null) q = q.Where(d => d.ReparacaoId == reparacaoId.Value);
        if (isRecorrente is not null) q = q.Where(d => d.IsRecorrente == isRecorrente.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(d =>
                EF.Functions.Like(d.Descricao, like) ||
                (d.Fornecedor != null && EF.Functions.Like(d.Fornecedor, like)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.Data)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(Despesa despesa, CancellationToken ct = default) => _db.Despesas.AddAsync(despesa, ct).AsTask();
    public void Remove(Despesa despesa) => _db.Despesas.Remove(despesa);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
