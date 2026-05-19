using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Backups;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/admin/backups")]
[Authorize(Roles = "Admin")]
public sealed class BackupsController : ControllerBase
{
    private readonly IBackupService _backup;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;

    public BackupsController(IBackupService backup, ITenantContext tenant, ICurrentUser user)
    {
        _backup = backup;
        _tenant = tenant;
        _user = user;
    }

    [HttpGet]
    [HttpGet("/api/admin/backup/list")]
    public Task<BackupListResult> List(CancellationToken ct) => _backup.ListAsync(ct);

    [HttpPost("now")]
    [HttpPost("/api/admin/backup/now")]
    public async Task<ActionResult<BackupRunResult>> RunNow(CancellationToken ct)
    {
        var result = await _backup.RunBackupAsync(BackupTrigger.Manual, ct);
        return Ok(result);
    }

    [HttpGet("{id}/restore-preview")]
    public async Task<ActionResult<BackupRestorePreviewDto>> RestorePreview(string id, CancellationToken ct)
    {
        if (!CanRestore())
            return Forbid();

        try
        {
            return Ok(await _backup.GetRestorePreviewAsync(id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<BackupRestoreResult>> Restore(
        string id,
        [FromBody] BackupRestoreRequest request,
        CancellationToken ct)
    {
        if (!CanRestore())
            return Forbid();

        try
        {
            BackupService.ValidateRestoreConfirmation(request);
            var result = await _backup.RestoreAsync(id, _tenant.TenantId, _user.UserId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "restore_confirmation_required")
        {
            return UnprocessableEntity(new
            {
                code = "restore_confirmation_required",
                message = "Escreve RESTORE para confirmar a reposicao.",
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private bool CanRestore() => _user.IsInRole("Admin") && _user.IsInRole("SuperAdmin");
}
