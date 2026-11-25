using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ISupplierInvoiceImportRepository
{
    Task<SupplierInvoiceImport?> FindBySha256Async(Guid tenantId, string sha256, CancellationToken ct = default);
    Task<SupplierInvoiceImport?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>25/11: importações (não rejeitadas) com o mesmo nº de documento do mesmo fornecedor, ou do mesmo email — para não ler 2× a mesma fatura (recibo, 2.º anexo).</summary>
    Task<IReadOnlyList<SupplierInvoiceImport>> ListParentesAsync(Guid tenantId, Guid? fornecedorId, string? documentNumber, string? emailMessageId, CancellationToken ct = default);
    /// <summary>Sprint 580: a fatura importada que deu origem a uma despesa (null se a despesa foi criada à mão).</summary>
    Task<SupplierInvoiceImport?> FindByDespesaIdAsync(Guid despesaId, CancellationToken ct = default);
    /// <summary>Sprint 586: despesa → fatura importada (PDF), para as despesas pedidas que têm uma.</summary>
    /// <summary>Sprint 688: dos imports dados, os que são de uma compra com despesas ligadas (fatura mista).</summary>
    Task<IReadOnlySet<Guid>> ImportsMistosAsync(IReadOnlyCollection<Guid> importIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Guid>> ImportIdsPorDespesaAsync(IReadOnlyCollection<Guid> despesaIds, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListPendingAsync(Guid tenantId, int take, CancellationToken ct = default);
    /// <summary>Sprint 163b: importações não-pending (Approved/Rejected/Failed) para tab "Histórico".</summary>
    Task<IReadOnlyList<SupplierInvoiceImport>> ListHistoryAsync(Guid tenantId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoiceImport>> ListByDateRangeAsync(
        Guid tenantId, DateTime from, DateTime to, SupplierInvoiceImportStatus? status, CancellationToken ct = default);
    Task AddAsync(SupplierInvoiceImport entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
