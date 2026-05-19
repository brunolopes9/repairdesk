using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Billing;

public sealed record TenantBillingSettingsDto(
    BillingProvider Provider,
    bool HasApiKey,
    string? ApiKeyMasked,
    string? ClientId,
    bool HasClientSecret,
    bool HasRefreshToken,
    int? CompanyId,
    BillingDocumentType DefaultDocumentType,
    int? DefaultSerieId,
    bool SandboxMode,
    int? DefaultProductId,
    int? DefaultTaxId,
    int? DefaultPaymentMethodId,
    int? DefaultMaturityDateId,
    int? FallbackCustomerId,
    string? ExemptionReason);

public sealed record UpdateTenantBillingSettingsRequest(
    BillingProvider Provider,
    string? ApiKey,
    string? ClientId,
    string? ClientSecret,
    string? RefreshToken,
    int? CompanyId,
    BillingDocumentType DefaultDocumentType,
    int? DefaultSerieId,
    bool SandboxMode,
    int? DefaultProductId,
    int? DefaultTaxId,
    int? DefaultPaymentMethodId,
    int? DefaultMaturityDateId,
    int? FallbackCustomerId,
    string? ExemptionReason);

public sealed record BillingConnectionTestDto(bool Success, string Message);

public sealed record BillingSerieDto(int Id, string Name, string? Code, bool IsActive);

public sealed record ConnectMoloniRequest(string Username, string Password);

public sealed record MoloniCompanyDto(int Id, string Name);

public sealed record MoloniProductDto(int Id, string Name, bool IsActive);

public sealed record MoloniTaxDto(int Id, string Name, decimal Value, bool IsActive, string? ExemptionReasonCode);

public sealed record MoloniPaymentMethodDto(int Id, string Name, bool IsActive);

public sealed record MoloniMaturityDateDto(int Id, string Name, bool IsActive);

public sealed record MoloniCustomerDto(int Id, string Name, string? Vat, bool IsActive);

public sealed record MoloniAutoDiscoverStepDto(
    string Key,
    string Label,
    bool Success,
    bool Created,
    int? Id,
    string? Name,
    string? Message);

public sealed record MoloniAutoDiscoverResultDto(
    int ProductsFound,
    int TaxesFound,
    int PaymentMethodsFound,
    int MaturityDatesFound,
    int CustomersFound,
    IReadOnlyList<MoloniAutoDiscoverStepDto> Steps,
    TenantBillingSettingsDto Settings);

public sealed record MoloniOAuthStartDto(string AuthorizationUrl, DateTime ExpiresAt);

public sealed record EmitInvoiceRequest(decimal? VatPercent, string? PaymentMethod);

public sealed record InvoiceDto(string Number, string? PdfUrl, DateTime EmittedAt);

public sealed record MoloniInvoiceResult(
    string ExternalId,
    string Number,
    string? PdfUrl,
    DateTime EmittedAt);
