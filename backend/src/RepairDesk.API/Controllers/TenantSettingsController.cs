using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.TenantSettings;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/tenant-settings")]
[Authorize]
public class TenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _service;
    public TenantSettingsController(ITenantSettingsService service) => _service = service;

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
}
