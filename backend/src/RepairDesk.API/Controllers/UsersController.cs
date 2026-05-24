using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly IRefreshTokenService _refresh;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _currentUser;

    public UsersController(
        UserManager<AppUser> users,
        IRefreshTokenService refresh,
        IAuditLogger audit,
        ITenantContext tenant,
        ICurrentUser currentUser)
    {
        _users = users;
        _refresh = refresh;
        _audit = audit;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    [HttpPost("{id:guid}/revoke-sessions")]
    public async Task<ActionResult<RevokeUserSessionsResponse>> RevokeSessions(Guid id, CancellationToken ct)
    {
        var user = await FindUserInCurrentTenantAsync(id);
        if (user is null) return NotFound();

        var revoked = await _refresh.RevokeAllForUserAsync(id, RequestIp(), ct);
        await _audit.LogAsync(
            AuditAction.Update,
            "AppUser",
            user.Id,
            new { action = "revoke_sessions", email = user.Email, revokedCount = revoked },
            user.TenantId,
            _currentUser.UserId,
            ct);

        return Ok(new RevokeUserSessionsResponse(revoked));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<DeactivateUserResponse>> Deactivate(Guid id, [FromBody] DeactivateUserRequest? request, CancellationToken ct)
    {
        var user = await FindUserInCurrentTenantAsync(id);
        if (user is null) return NotFound();

        user.IsActive = false;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                code = "user_deactivate_failed",
                errors = result.Errors.Select(e => e.Code).ToList()
            });
        }

        var revoked = await _refresh.RevokeAllForUserAsync(id, RequestIp(), ct);
        await _audit.LogAsync(
            AuditAction.UserDeactivated,
            "AppUser",
            user.Id,
            new { email = user.Email, reason = request?.Reason, revokedCount = revoked },
            user.TenantId,
            _currentUser.UserId,
            ct);

        return Ok(new DeactivateUserResponse(user.Id, revoked));
    }

    private async Task<AppUser?> FindUserInCurrentTenantAsync(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return null;
        return _tenant.TenantId == user.TenantId ? user : null;
    }

    private string? RequestIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}

public sealed record RevokeUserSessionsResponse(int RevokedCount);
public sealed record DeactivateUserRequest(string? Reason);
public sealed record DeactivateUserResponse(Guid UserId, int RevokedCount);
