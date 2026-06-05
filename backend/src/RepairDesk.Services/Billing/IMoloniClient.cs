using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Billing;

public interface IMoloniClient
{
    Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default);
    Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default);
    Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default);
    Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default);
    Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default);
    Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(TenantBillingSettings settings, int estimateId, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default);

    // Emite Nota de Credito Moloni que anula a fatura original (saldo IVA = 0).
    // O reference parameter aponta à fatura original via related_documents.
    Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default);

    // Cancela documento Moloni directamente (status -> Anulado, sem criar 2º documento).
    // Restricoes Moloni: so funciona se status=fechado, nao pendente AT, sem codigo AT associado,
    // nao gerou outros documentos. Para faturas simplificadas geralmente funciona.
    // Devolve true se cancelado com sucesso, false se Moloni rejeitou (chamar InsertCreditNote como fallback).
    Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default);

    // Devolve status do documento Moloni: 0=Rascunho, 1=Fechado, 2=Anulado, ou null se inexistente/erro.
    // Permite sync RepairDesk DB com fonte de verdade fiscal (Moloni). Caller deve tratar null
    // como 'nao conseguimos verificar — manter estado local'.
    Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default);

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
    Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default);
    Task<bool> UpdateCustomerAsync(TenantBillingSettings settings, int customerId, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default);

    // Sprint 514: lista os documentos de venda emitidos no Moloni (documents/getAll, todos os tipos).
    // Backbone do "fetch de anteriores" para a lista única de Faturas — traz histórico + NCs +
    // documentos feitos directamente no painel Moloni, com valores e estado reais.
    Task<IReadOnlyList<MoloniDocumentRow>> ListDocumentsAsync(TenantBillingSettings settings, CancellationToken ct = default);
}

/// <summary>Sprint 514: documento de venda devolvido pelo Moloni documents/getAll. Valores em cêntimos.</summary>
public sealed record MoloniDocumentRow(
    int DocumentId,
    string? SaftCode,      // FT | FS | FR | NC | ND | VD | …
    string Numero,         // ex: "FT 2026/2"
    DateTime Data,
    string? EntityName,
    string? EntityVat,
    int GrossCents,        // total com IVA (gross_value)
    int NetCents,          // base sem IVA (net_value)
    int TaxesCents,        // IVA (taxes_value)
    int Status);           // 0=Rascunho, 1=Fechado, 2=Anulado

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
