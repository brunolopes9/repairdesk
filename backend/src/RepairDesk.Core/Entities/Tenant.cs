using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Tenant : BaseEntity
{
    public required string Name { get; set; }
    public string? LegalName { get; set; }
    public string? Nif { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Locality { get; set; }
    public string? Country { get; set; } = "PT";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Iban { get; set; }
    public string? CaePrincipal { get; set; }
    public string? CaeSecundarios { get; set; } // CSV de códigos: "47401,58290,95101,95102"
    public RegimeFiscal RegimeFiscal { get; set; } = RegimeFiscal.IsentoArt53;
    public string? TermosCondicoes { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OnboardingCompletado { get; set; }

    // Garantia (defaults aplicados quando uma reparação é entregue)
    public int GarantiaDiasDefault { get; set; } = 90;
    public string? GarantiaCoberturaDefault { get; set; }
    public string? GarantiaExclusoesDefault { get; set; }

    // Google Reviews funil (mostrado ao cliente quando avalia 4-5 estrelas)
    public string? GoogleReviewUrl { get; set; }

    public TenantBillingSettings? BillingSettings { get; set; }
}
