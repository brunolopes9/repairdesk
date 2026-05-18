using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Backups;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/admin/backup")]
[Authorize(Roles = "Admin")]
public sealed class AdminBackupController : ControllerBase
{
    private readonly IBackupService _backup;

    public AdminBackupController(IBackupService backup)
    {
        _backup = backup;
    }

    [HttpPost("now")]
    public async Task<ActionResult<BackupRunResult>> RunNow(CancellationToken ct)
    {
        var result = await _backup.RunBackupAsync(BackupTrigger.Manual, ct);
        return Ok(result);
    }

    [HttpGet("list")]
    public Task<BackupListResult> List(CancellationToken ct) => _backup.ListAsync(ct);
}
