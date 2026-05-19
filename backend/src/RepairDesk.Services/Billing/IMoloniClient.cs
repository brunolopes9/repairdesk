using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Billing;

public interface IMoloniClient
{
    Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default);
    Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default);

    // Emite Nota de Credito Moloni que anula a fatura original (saldo IVA = 0).
    // O reference parameter aponta à fatura original via related_documents.
    Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default);

    // Cancela documento Moloni directamente (status -> Anulado, sem criar 2º documento).
    // Restricoes Moloni: so funciona se status=fechado, nao pendente AT, sem codigo AT associado,
    // nao gerou outros documentos. Para faturas simplificadas geralmente funciona.
    // Devolve true se cancelado com sucesso, false se Moloni rejeitou (chamar InsertCreditNote como fallback).
    Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default);

    // OAuth2 password grant: troca username+password (uma vez) por tokens. Tokens guardados cifrados em settings; password nunca persistida.
    Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default);
    Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default);

    // Auto-descoberta de empresas disponíveis na conta Moloni autenticada.
    Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default);
    Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default);
}

public sealed record MoloniInvoiceDraft(
    int CustomerId,
    string Reference,
    string ItemName,
    string? Summary,
    int AmountCents,
    decimal VatPercent,
    string? PaymentMethod,
    BillingDocumentType? DocumentTypeOverride = null,
    IReadOnlyList<MoloniInvoiceDraftItem>? Items = null);

public sealed record MoloniCreditNoteDraft(
    int OriginalDocumentId,
    int CustomerId,
    string Reference,
    IReadOnlyList<MoloniInvoiceDraftItem> Items,
    string Motivo);

public sealed record MoloniInvoiceDraftItem(
    string Name,
    string? Summary,
    int Quantity,
    int UnitPriceCents,
    int DiscountCents,
    decimal VatPercent);
