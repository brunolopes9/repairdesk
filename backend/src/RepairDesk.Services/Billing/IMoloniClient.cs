using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Billing;

public interface IMoloniClient
{
    Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default);
    Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default);
}

public sealed record MoloniInvoiceDraft(
    int CustomerId,
    string Reference,
    string ItemName,
    string? Summary,
    int AmountCents,
    decimal VatPercent,
    string? PaymentMethod);
