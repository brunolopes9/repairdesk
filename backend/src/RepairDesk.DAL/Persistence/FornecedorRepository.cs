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

        // Compras de stock: entradas × custo, match por Part.Fornecedor (string snapshot — a mesma
        // convenção do Top Fornecedores do relatório Negócio).
        var comprasStock = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.Quantidade > 0
                && m.Motivo == Core.Enums.PartMovimentoMotivo.Entrada
                && m.Part != null && m.Part.Fornecedor == f.Name)
            .SumAsync(m => (long?)(m.Quantidade * m.Part!.CustoUnitarioCents), ct) ?? 0;

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

        // Taxa de defeito a 12 meses — mesma técnica do GetTaxaDefeitoFornecedorAsync: cruzar em
        // memória os IMEIs vendidos deste fornecedor com reparações posteriores (volume pequeno).
        var desde = DateTime.UtcNow.AddMonths(-12);
        var vendidos = await _db.VendaItems
            .AsNoTracking()
            .Where(vi => vi.FornecedorNome == f.Name && vi.Imei != null && vi.Venda!.Data >= desde)
            .Select(vi => new { vi.Imei, DataVenda = vi.Venda!.Data })
            .ToListAsync(ct);
        var comReparacao = 0;
        if (vendidos.Count > 0)
        {
            var imeis = vendidos.Select(v => v.Imei!).Distinct().ToList();
            var minCreatedByImei = (await _db.Reparacoes
                    .AsNoTracking()
                    .Where(r => r.Imei != null && imeis.Contains(r.Imei))
                    .GroupBy(r => r.Imei!)
                    .Select(g => new { Imei = g.Key, MinCreatedAt = g.Min(r => r.CreatedAt) })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Imei, x => x.MinCreatedAt);
            comReparacao = vendidos.Count(v =>
                minCreatedByImei.TryGetValue(v.Imei!, out var min) && min > v.DataVenda);
        }
        var taxa = vendidos.Count == 0
            ? 0m
            : Math.Round(comReparacao * 100m / vendidos.Count, 2, MidpointRounding.AwayFromZero);

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
            ItensVendidos12m: vendidos.Count,
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
