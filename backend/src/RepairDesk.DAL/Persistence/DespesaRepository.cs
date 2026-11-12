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

    public async Task<IReadOnlyList<Despesa>> FindByNumerosAsync(IReadOnlyCollection<int> numeros, CancellationToken ct = default)
        => await _db.Despesas.Where(d => numeros.Contains(d.Numero)).ToListAsync(ct);

    public async Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        bool? isRecorrente, int page, int pageSize, CancellationToken ct = default, DespesaAmbito? ambito = null)
    {
        var q = Filtrar(query, categoria, categoriaIn, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente, ambito);
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
        bool? isRecorrente, CancellationToken ct = default, DespesaAmbito? ambito = null)
        => await Filtrar(query, categoria, categoriaIn, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, isRecorrente, ambito)
            .GroupBy(d => d.Categoria)
            .Select(g => new DespesaResumoCategoria(g.Key, g.Count(), g.Sum(d => d.ValorCents),
                g.Sum(d => d.IvaCents ?? 0), g.Count(d => d.IvaCents == null && !d.ReverseCharge), g.Count(d => d.IsRecorrente)))
            .ToListAsync(ct);

    private IQueryable<Despesa> Filtrar(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to, bool? isRecorrente,
        DespesaAmbito? ambito = null)
    {
        var q = _db.Despesas.AsNoTracking().AsQueryable();
        if (ambito is { } a) q = q.Where(d => d.Ambito == a);
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
            // Sprint 584: também pelo nº da despesa e pelo nº do documento do fornecedor.
            // Sprint 591: por palavras, sem olhar a acentos.
            var numero = int.TryParse(query.Trim(), out var n) ? n : -1;
            var termos = PesquisaTexto.Termos(query);
            var t0 = PesquisaTexto.Padrao(query.Trim());
            foreach (var termo in termos)
            {
                var t = termo;
                q = q.Where(d => d.Numero == numero || PesquisaTexto.Contem(d.NumeroEncomenda, t0)
                    || PesquisaTexto.Contem(d.Descricao, t) || PesquisaTexto.Contem(d.Fornecedor, t));
            }
        }
        return q;
    }

    public Task AddAsync(Despesa despesa, CancellationToken ct = default) => _db.Despesas.AddAsync(despesa, ct).AsTask();
    public void Remove(Despesa despesa) => _db.Despesas.Remove(despesa);
    /// <summary>
    /// Sprint 584: grava e atribui o nº visível (por tenant) às despesas novas. Dois pedidos em simultâneo podem
    /// calcular o mesmo nº: o índice único rejeita um deles e tenta-se outra vez (como nas compras).
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var novas = _db.ChangeTracker.Entries<Despesa>()
                .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
            // O TenantId só é carimbado no SaveChanges — aqui usa-se já o do contexto para contar no tenant certo.
            foreach (var d in novas.Where(d => d.TenantId == Guid.Empty && _db.CurrentTenantId is not null)) d.TenantId = _db.CurrentTenantId!.Value;
            foreach (var grupo in novas.GroupBy(d => d.TenantId))
            {
                var max = await _db.Despesas.IgnoreQueryFilters().Where(d => d.TenantId == grupo.Key)
                    .Select(d => (int?)d.Numero).MaxAsync(ct) ?? 0;
                foreach (var d in grupo.OrderBy(d => d.Data)) d.Numero = ++max;
            }
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && novas.Count > 0)
            {
                foreach (var d in novas) d.Numero = 0;
            }
        }
    }
}
