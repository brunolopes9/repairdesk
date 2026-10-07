using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class FornecedorRepository : IFornecedorRepository
{
    private readonly AppDbContext _db;
    public FornecedorRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Fornecedor>> ListByTenantAsync(bool includeInactive, CancellationToken ct = default)
        => await _db.Fornecedores
            .AsNoTracking()
            .Where(f => includeInactive || f.Active)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    public Task<Fornecedor?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<Fornecedor?> FindByNameAsync(string name, CancellationToken ct = default)
        => _db.Fornecedores.FirstOrDefaultAsync(f => f.Name == name, ct);

    public Task AddAsync(Fornecedor f, CancellationToken ct = default)
        => _db.Fornecedores.AddAsync(f, ct).AsTask();

    public void Remove(Fornecedor f) => _db.Fornecedores.Remove(f);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<FornecedorHistorico?> GetHistoricoAsync(Guid id, CancellationToken ct = default)
    {
        var f = await _db.Fornecedores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return null;

        // Compras de stock (Doc 94 Fase 3): linhas dos documentos de compra deste fornecedor, em cêntimos.
        var comprasStockEuros = await _db.ComprasLinhas
            .AsNoTracking()
            .Where(l => l.Documento != null && l.Documento.FornecedorId == id)
            .SumAsync(l => (decimal?)(l.Quantidade * l.PrecoUnitarioPago), ct) ?? 0m;
        var comprasStock = (long)Math.Round(comprasStockEuros * 100m, MidpointRounding.AwayFromZero);

        var despesas = await _db.Despesas
            .AsNoTracking()
            .Where(d => d.Fornecedor == f.Name && !d.IsCogs)
            .SumAsync(d => (long?)d.ValorCents, ct) ?? 0;

        var imports = await _db.SupplierInvoiceImports
            .AsNoTracking()
            .Where(i => i.FornecedorId == id)
            .Select(i => new { i.Id, i.ParsedDocumentNumber, i.ParsedDocumentDate, i.ParsedTotalCents, i.Status, i.CreatedAt })
            .ToListAsync(ct);

        var ultimaCompra = imports
            .Select(i => (DateTime?)(i.ParsedDocumentDate ?? i.CreatedAt))
            .DefaultIfEmpty(null)
            .Max();

        // Unidades deste fornecedor vendidas nos últimos 12 meses (via lotes). A taxa de defeito por IMEI
        // dependia das Reparações antigas — fica a 0 até existir registo de garantias por lote.
        var desde = DateTime.UtcNow.AddMonths(-12);
        var vendidosCount = await _db.VendaItems
            .AsNoTracking()
            .Where(vi => vi.CompraLinha != null && vi.CompraLinha.Documento != null
                && vi.CompraLinha.Documento.FornecedorId == id
                && vi.Venda != null && vi.Venda.Estado == Core.Enums.VendaEstado.Entregue && vi.Venda.Data >= desde)
            .SumAsync(vi => (int?)vi.Quantidade, ct) ?? 0;
        const int comReparacao = 0;
        const decimal taxa = 0m;

        return new FornecedorHistorico(
            f.Id,
            f.Name,
            f.IntraUe,
            f.DefaultImportAction.ToString().ToLowerInvariant(),
            (int?)f.DefaultDespesaCategoria,
            f.GarantiaB2BDiasDefault,
            comprasStock,
            despesas,
            ImportsTotal: imports.Count,
            ImportsPendentes: imports.Count(i => i.Status == SupplierInvoiceImportStatus.Pending),
            ultimaCompra,
            ItensVendidos12m: vendidosCount,
            ItensComReparacao12m: comReparacao,
            TaxaDefeitoPct12m: taxa,
            UltimasFaturas: imports
                .OrderByDescending(i => i.ParsedDocumentDate ?? i.CreatedAt)
                .Take(8)
                .Select(i => new FornecedorFaturaResumo(
                    i.Id, i.ParsedDocumentNumber, i.ParsedDocumentDate ?? i.CreatedAt,
                    i.ParsedTotalCents, i.Status.ToString()))
                .ToList());
    }
}
