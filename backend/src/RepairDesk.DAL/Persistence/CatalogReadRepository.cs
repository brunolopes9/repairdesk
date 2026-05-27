using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.DAL.Persistence;

/// <summary>
/// Sprint 385 (Doc 87): carga agregada read-only para o "Catálogo &amp; Stock". Tenant scoping é
/// garantido pelos global query filters do <see cref="AppDbContext"/>. Catálogos de loja de
/// reparações são pequenos (dezenas a poucas centenas) — uma carga completa por pedido é aceitável;
/// o agrupamento pai→variante e os filtros das tabs fazem-se no serviço.
/// </summary>
public sealed class CatalogReadRepository : ICatalogReadRepository
{
    private readonly AppDbContext _db;
    public CatalogReadRepository(AppDbContext db) => _db = db;

    public async Task<CatalogReadData> LoadAsync(CancellationToken ct = default)
    {
        var models = await _db.ProductModels
            .AsNoTracking()
            .Include(m => m.Images)
            .OrderBy(m => m.Brand).ThenBy(m => m.Model)
            .ToListAsync(ct);

        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Fornecedor)
            .OrderBy(p => p.Brand).ThenBy(p => p.Model)
            .ToListAsync(ct);

        var parts = await _db.Parts
            .AsNoTracking()
            .OrderBy(p => p.Marca).ThenBy(p => p.Modelo).ThenBy(p => p.Nome)
            .ToListAsync(ct);

        return new CatalogReadData(models, products, parts);
    }
}
