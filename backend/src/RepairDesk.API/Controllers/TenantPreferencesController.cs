using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/tenant-settings/me/preferences")]
[Authorize]
public class TenantPreferencesController : ControllerBase
{
    private readonly ITenantPreferencesService _service;

    public TenantPreferencesController(ITenantPreferencesService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<TenantPreferencesRoot> Get(CancellationToken ct)
        => _service.GetAsync(ct);

    [HttpPut]
    public Task<TenantPreferencesRoot> Update([FromBody] TenantPreferencesRoot request, CancellationToken ct)
        => _service.UpdateAsync(request, ct);

    [HttpPost("reset/{group}")]
    public Task<TenantPreferencesRoot> ResetGroup(string group, CancellationToken ct)
        => _service.ResetGroupAsync(group, ct);
}
