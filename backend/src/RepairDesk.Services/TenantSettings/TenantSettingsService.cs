using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.TenantSettings;

public interface ITenantSettingsService
{
    Task<TenantSettingsDto> GetMineAsync(CancellationToken ct = default);
    Task<TenantSettingsDto> UpdateMineAsync(UpdateTenantSettingsRequest req, CancellationToken ct = default);
    Task<OnboardingStatusDto> GetOnboardingStatusAsync(CancellationToken ct = default);
    Task<OnboardingStatusDto> CompleteOnboardingAsync(CancellationToken ct = default);
}

public class TenantSettingsService : ITenantSettingsService
{
    private readonly ITenantRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantContext _tenantContext;

    public TenantSettingsService(
        ITenantRepository repo,
        IClienteRepository clientes,
        IReparacaoRepository reparacoes,
        ITenantContext tenantContext)
    {
        _repo = repo;
        _clientes = clientes;
        _reparacoes = reparacoes;
        _tenantContext = tenantContext;
    }

    public async Task<TenantSettingsDto> GetMineAsync(CancellationToken ct = default)
    {
        var tenant = await RequireCurrentTenantAsync(ct);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateMineAsync(UpdateTenantSettingsRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("name_required", "Nome obrigatório.");

        var nifClean = Clean(req.Nif);
        if (!string.IsNullOrWhiteSpace(nifClean) && !NifValidator.IsValid(nifClean))
            throw new ValidationException("nif_invalid", "NIF inválido — verifica os 9 dígitos e o check-digit.");

        var tenant = await RequireCurrentTenantAsync(ct);

        tenant.Name = req.Name.Trim();
        tenant.LegalName = Clean(req.LegalName);
        tenant.Nif = nifClean;
        tenant.Address = Clean(req.Address);
        tenant.PostalCode = Clean(req.PostalCode);
        tenant.Locality = Clean(req.Locality);
        tenant.Country = string.IsNullOrWhiteSpace(req.Country) ? "PT" : req.Country.Trim().ToUpperInvariant();
        tenant.Phone = Clean(req.Phone);
        tenant.Email = Clean(req.Email);
        tenant.Website = Clean(req.Website);
        tenant.Iban = Clean(req.Iban)?.Replace(" ", "").ToUpperInvariant();
        tenant.CaePrincipal = Clean(req.CaePrincipal);
        tenant.CaeSecundarios = Clean(req.CaeSecundarios);
        tenant.RegimeFiscal = req.RegimeFiscal;
        tenant.TermosCondicoes = Clean(req.TermosCondicoes);
        tenant.LogoUrl = Clean(req.LogoUrl);
        tenant.PrimaryColor = Clean(req.PrimaryColor);
        tenant.GarantiaDiasDefault = Math.Clamp(req.GarantiaDiasDefault <= 0 ? 90 : req.GarantiaDiasDefault, 1, 3650);
        tenant.GarantiaCoberturaDefault = Clean(req.GarantiaCoberturaDefault);
        tenant.GarantiaExclusoesDefault = Clean(req.GarantiaExclusoesDefault);
        // DL 84/2021: 3 anos (1095 dias) minimo legal para consumo; 18 meses (540) o minimo
        // permitido em refurbished se contratualizado expressamente.
        tenant.GarantiaVendaDiasDefault = Math.Clamp(req.GarantiaVendaDiasDefault <= 0 ? 1095 : req.GarantiaVendaDiasDefault, 540, 3650);
        tenant.GarantiaVendaCoberturaDefault = Clean(req.GarantiaVendaCoberturaDefault);
        tenant.GarantiaVendaExclusoesDefault = Clean(req.GarantiaVendaExclusoesDefault);
        tenant.GoogleReviewUrl = Clean(req.GoogleReviewUrl);

        await _repo.SaveAsync(ct);
        return ToDto(tenant);
    }

    public async Task<OnboardingStatusDto> GetOnboardingStatusAsync(CancellationToken ct = default)
    {
        var tenant = await RequireCurrentTenantAsync(ct);
        return await BuildOnboardingStatusAsync(tenant, ct);
    }

    public async Task<OnboardingStatusDto> CompleteOnboardingAsync(CancellationToken ct = default)
    {
        var tenant = await RequireCurrentTenantAsync(ct);
        tenant.OnboardingCompletado = true;
        await _repo.SaveAsync(ct);
        return await BuildOnboardingStatusAsync(tenant, ct);
    }

    private async Task<Tenant> RequireCurrentTenantAsync(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var tenant = await _repo.FindByIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);
        return tenant;
    }

    private static string? Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<OnboardingStatusDto> BuildOnboardingStatusAsync(Tenant tenant, CancellationToken ct)
    {
        var empresaCompleta = !string.IsNullOrWhiteSpace(tenant.Name);
        var clienteCriado = await _clientes.AnyAsync(ct);
        var reparacaoCriada = await _reparacoes.AnyAsync(ct);
        var completed = tenant.OnboardingCompletado;
        var currentStep = completed
            ? 5
            : !empresaCompleta
                ? 1
                : !clienteCriado
                    ? 2
                    : !reparacaoCriada
                        ? 3
                        : 4;

        return new OnboardingStatusDto(
            completed,
            empresaCompleta,
            clienteCriado,
            reparacaoCriada,
            DashboardVisto: completed,
            EquipaConvidada: completed,
            CurrentStep: currentStep,
            TotalSteps: 5);
    }

    private static TenantSettingsDto ToDto(Tenant t) => new(
        t.Id,
        t.Name,
        t.LegalName,
        t.Nif,
        t.Address,
        t.PostalCode,
        t.Locality,
        t.Country,
        t.Phone,
        t.Email,
        t.Website,
        t.Iban,
        t.CaePrincipal,
        t.CaeSecundarios,
        t.RegimeFiscal,
        t.TermosCondicoes,
        t.LogoUrl,
        t.PrimaryColor,
        t.OnboardingCompletado,
        t.GarantiaDiasDefault,
        t.GarantiaCoberturaDefault,
        t.GarantiaExclusoesDefault,
        t.GarantiaVendaDiasDefault,
        t.GarantiaVendaCoberturaDefault,
        t.GarantiaVendaExclusoesDefault,
        t.GoogleReviewUrl);
}
