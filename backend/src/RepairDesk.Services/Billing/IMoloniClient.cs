using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Billing;

public interface IMoloniClient
{
    Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default);
    Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default);

    // OAuth2 password grant: troca username+password (uma vez) por tokens. Tokens guardados cifrados em settings; password nunca persistida.
    Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default);

    // Auto-descoberta de empresas disponíveis na conta Moloni autenticada.
    Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default);
}

public sealed record MoloniInvoiceDraft(
    int CustomerId,
    string Reference,
    string ItemName,
    string? Summary,
    int AmountCents,
    decimal VatPercent,
    string? PaymentMethod);
