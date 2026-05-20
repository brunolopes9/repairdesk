using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ISupplierInvoiceImportRepository
{
    Task<SupplierInvoiceImport?> FindBySha256Async(Guid tenantId, string sha256, CancellationToken ct = default);
    Task<SupplierInvoiceImport?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListPendingAsync(Guid tenantId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListByDateRangeAsync(
        Guid tenantId, DateTime from, DateTime to, SupplierInvoiceImportStatus? status, CancellationToken ct = default);
    Task AddAsync(SupplierInvoiceImport entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
