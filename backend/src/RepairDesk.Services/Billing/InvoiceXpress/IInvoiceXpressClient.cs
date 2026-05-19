using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Billing.InvoiceXpress;

public interface IInvoiceXpressClient
{
    Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceXpressClientDto>> GetClientsAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceXpressItemDto>> GetItemsAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceXpressDocumentDto>> ListInvoicesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<InvoiceXpressInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, InvoiceXpressInvoiceDraft draft, CancellationToken ct = default);
    Task<InvoiceXpressInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, InvoiceXpressCreditNoteDraft draft, CancellationToken ct = default);
    Task<bool> CancelDocumentAsync(TenantBillingSettings settings, string externalId, string reason, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string externalId, CancellationToken ct = default);
}

public sealed record InvoiceXpressClientDto(int Id, string Name, string? FiscalId);

public sealed record InvoiceXpressItemDto(int Id, string Name);

public sealed record InvoiceXpressDocumentDto(int Id, string Number, string Status, string Type);

public sealed record InvoiceXpressInvoiceResult(
    string ExternalId,
    string Number,
    string? PdfUrl,
    DateTime EmittedAt);

public sealed record InvoiceXpressInvoiceDraft(
    InvoiceXpressClientDraft Client,
    string Reference,
    string ItemName,
    string? Summary,
    int AmountCents,
    decimal VatPercent,
    string? PaymentMethod,
    BillingDocumentType? DocumentTypeOverride = null,
    IReadOnlyList<InvoiceXpressInvoiceDraftItem>? Items = null);

public sealed record InvoiceXpressCreditNoteDraft(
    string OriginalExternalId,
    InvoiceXpressClientDraft Client,
    string Reference,
    IReadOnlyList<InvoiceXpressInvoiceDraftItem> Items,
    string Reason);

public sealed record InvoiceXpressClientDraft(
    string Name,
    string? Email,
    string? FiscalId,
    string? Phone);

public sealed record InvoiceXpressInvoiceDraftItem(
    string Name,
    string? Summary,
    int Quantity,
    int UnitPriceCents,
    int DiscountCents,
    decimal VatPercent);
