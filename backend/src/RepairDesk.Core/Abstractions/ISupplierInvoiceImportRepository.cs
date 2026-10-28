using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ISupplierInvoiceImportRepository
{
    Task<SupplierInvoiceImport?> FindBySha256Async(Guid tenantId, string sha256, CancellationToken ct = default);
    Task<SupplierInvoiceImport?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sprint 580: a fatura importada que deu origem a uma despesa (null se a despesa foi criada à mão).</summary>
    Task<SupplierInvoiceImport?> FindByDespesaIdAsync(Guid despesaId, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListPendingAsync(Guid tenantId, int take, CancellationToken ct = default);
    /// <summary>Sprint 163b: importações não-pending (Approved/Rejected/Failed) para tab "Histórico".</summary>
    Task<IReadOnlyList<SupplierInvoiceImport>> ListHistoryAsync(Guid tenantId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListByDateRangeAsync(
        Guid tenantId, DateTime from, DateTime to, SupplierInvoiceImportStatus? status, CancellationToken ct = default);
    Task AddAsync(SupplierInvoiceImport entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
