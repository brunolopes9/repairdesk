using RepairDesk.Core.Enums;

namespace RepairDesk.Services.TenantSettings;

public sealed record TenantSettingsDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? Nif,
    string? Address,
    string? PostalCode,
    string? Locality,
    string? Country,
    string? Phone,
    string? Email,
    string? Website,
    string? Iban,
    string? CaePrincipal,
    string? CaeSecundarios,
    RegimeFiscal RegimeFiscal,
    string? TermosCondicoes,
    string? LogoUrl,
    string? PrimaryColor,
    bool OnboardingCompletado,
    int GarantiaDiasDefault,
    string? GarantiaCoberturaDefault,
    string? GarantiaExclusoesDefault,
    string? GoogleReviewUrl);

public sealed record UpdateTenantSettingsRequest(
    string Name,
    string? LegalName,
    string? Nif,
    string? Address,
    string? PostalCode,
    string? Locality,
    string? Country,
    string? Phone,
    string? Email,
    string? Website,
    string? Iban,
    string? CaePrincipal,
    string? CaeSecundarios,
    RegimeFiscal RegimeFiscal,
    string? TermosCondicoes,
    string? LogoUrl,
    string? PrimaryColor,
    int GarantiaDiasDefault,
    string? GarantiaCoberturaDefault,
    string? GarantiaExclusoesDefault,
    string? GoogleReviewUrl);

public sealed record OnboardingStatusDto(
    bool OnboardingCompletado,
    bool EmpresaCompleta,
    bool ClienteCriado,
    bool ReparacaoCriada,
    bool DashboardVisto,
    bool EquipaConvidada,
    int CurrentStep,
    int TotalSteps);
