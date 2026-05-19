using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Billing;
using RepairDesk.Services.TenantSettings;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/tenant-settings")]
[Authorize]
public class TenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _service;
    private readonly ITenantBillingSettingsService _billing;

    public TenantSettingsController(ITenantSettingsService service, ITenantBillingSettingsService billing)
    {
        _service = service;
        _billing = billing;
    }

    [HttpGet("me")]
    public Task<TenantSettingsDto> GetMine(CancellationToken ct) => _service.GetMineAsync(ct);

    [HttpPut("me")]
    public Task<TenantSettingsDto> UpdateMine([FromBody] UpdateTenantSettingsRequest req, CancellationToken ct)
        => _service.UpdateMineAsync(req, ct);

    [HttpGet("me/onboarding/status")]
    public Task<OnboardingStatusDto> GetOnboardingStatus(CancellationToken ct)
        => _service.GetOnboardingStatusAsync(ct);

    [HttpPost("me/onboarding/complete")]
    public Task<OnboardingStatusDto> CompleteOnboarding(CancellationToken ct)
        => _service.CompleteOnboardingAsync(ct);

    [HttpGet("me/billing")]
    public Task<TenantBillingSettingsDto> GetBilling(CancellationToken ct)
        => _billing.GetMineAsync(ct);

    [HttpPut("me/billing")]
    public Task<TenantBillingSettingsDto> UpdateBilling([FromBody] UpdateTenantBillingSettingsRequest req, CancellationToken ct)
        => _billing.UpdateMineAsync(req, ct);

    [HttpPost("me/billing/test-connection")]
    public Task<BillingConnectionTestDto> TestBillingConnection(CancellationToken ct)
        => _billing.TestConnectionAsync(ct);

    [HttpPost("me/billing/sync-series")]
    public Task<IReadOnlyList<BillingSerieDto>> SyncBillingSeries(CancellationToken ct)
        => _billing.SyncSeriesAsync(ct);

    [HttpPost("me/billing/moloni/connect")]
    public Task<TenantBillingSettingsDto> ConnectMoloni([FromBody] ConnectMoloniRequest req, CancellationToken ct)
        => _billing.ConnectMoloniAsync(req, ct);

    [HttpPost("me/billing/moloni/disconnect")]
    public Task<TenantBillingSettingsDto> DisconnectMoloni(CancellationToken ct)
        => _billing.DisconnectMoloniAsync(ct);

    [HttpGet("me/billing/moloni/companies")]
    public Task<IReadOnlyList<MoloniCompanyDto>> ListMoloniCompanies(CancellationToken ct)
        => _billing.ListCompaniesAsync(ct);

    [HttpPost("me/billing/moloni/auto-discover")]
    public Task<MoloniAutoDiscoverResultDto> AutoDiscoverMoloni(CancellationToken ct)
        => _billing.AutoDiscoverAsync(ct);
}
