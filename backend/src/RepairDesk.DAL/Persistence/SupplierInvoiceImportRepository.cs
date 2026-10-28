using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class SupplierInvoiceImportRepository : ISupplierInvoiceImportRepository
{
    private readonly AppDbContext _db;

    public SupplierInvoiceImportRepository(AppDbContext db) => _db = db;

    public Task<SupplierInvoiceImport?> FindBySha256Async(Guid tenantId, string sha256, CancellationToken ct = default)
        => _db.SupplierInvoiceImports
            .Where(x => x.TenantId == tenantId && x.PdfSha256 == sha256)
            .FirstOrDefaultAsync(ct);

    public Task<SupplierInvoiceImport?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.SupplierInvoiceImports
            .Include(x => x.Fornecedor)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SupplierInvoiceImport?> FindByDespesaIdAsync(Guid despesaId, CancellationToken ct = default)
        => _db.SupplierInvoiceImports.FirstOrDefaultAsync(x => x.DespesaId == despesaId, ct);

    public async Task<IReadOnlyList<SupplierInvoiceImport>> ListPendingAsync(Guid tenantId, int take, CancellationToken ct = default)
    {
        return await _db.SupplierInvoiceImports
            .Include(x => x.Fornecedor)
            .Where(x => x.TenantId == tenantId
                && (x.Status == SupplierInvoiceImportStatus.Pending || x.Status == SupplierInvoiceImportStatus.Failed))
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierInvoiceImport>> ListHistoryAsync(Guid tenantId, int take, CancellationToken ct = default)
    {
        return await _db.SupplierInvoiceImports
            .Include(x => x.Fornecedor)
            .Where(x => x.TenantId == tenantId
                && (x.Status == SupplierInvoiceImportStatus.Approved || x.Status == SupplierInvoiceImportStatus.Rejected))
            .OrderByDescending(x => x.ProcessedAt ?? x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierInvoiceImport>> ListByDateRangeAsync(
        Guid tenantId, DateTime from, DateTime to, SupplierInvoiceImportStatus? status, CancellationToken ct = default)
    {
        var q = _db.SupplierInvoiceImports
            .Include(x => x.Fornecedor)
            // Sprint 563: pela data do documento (a que conta para o IVA), não pela data em que foi carregado.
            .Where(x => x.TenantId == tenantId
                && (x.ParsedDocumentDate ?? x.CreatedAt) >= from && (x.ParsedDocumentDate ?? x.CreatedAt) < to);
        if (status is not null) q = q.Where(x => x.Status == status.Value);
        return await q.OrderBy(x => x.ParsedDocumentDate ?? x.CreatedAt).ToListAsync(ct);
    }

    public Task AddAsync(SupplierInvoiceImport entity, CancellationToken ct = default)
        => _db.SupplierInvoiceImports.AddAsync(entity, ct).AsTask();

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
