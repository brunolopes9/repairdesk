using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class TenantBillingSettings : BaseEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public BillingProvider Provider { get; set; } = BillingProvider.None;

    public string? ApiKeyCipherText { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecretCipherText { get; set; }
    public string? RefreshTokenCipherText { get; set; }

    public int? CompanyId { get; set; }
    public BillingDocumentType DefaultDocumentType { get; set; } = BillingDocumentType.FaturaSimplificada;
    public int? DefaultSerieId { get; set; }
    public bool SandboxMode { get; set; }

    public int? DefaultProductId { get; set; }
    public int? DefaultTaxId { get; set; }
    public int? DefaultPaymentMethodId { get; set; }
    public int? DefaultMaturityDateId { get; set; }
    public int? FallbackCustomerId { get; set; }
    public string? ExemptionReason { get; set; }
}
