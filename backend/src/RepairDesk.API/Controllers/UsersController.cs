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

    /// <summary>
    /// Sprint 311 (Doc 72 Fase D): substitui o conjunto completo de roles do user pelos
    /// passados no request. Apenas roles em <see cref="RepairDesk.Core.Auth.AppRoles.All"/>
    /// são aceites. Admin não pode auto-remover Admin (último Admin do tenant fica protegido).
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<UserRolesResponse>> SetRoles(Guid id, [FromBody] SetUserRolesRequest req, CancellationToken ct)
    {
        var user = await FindUserInCurrentTenantAsync(id);
        if (user is null) return NotFound();

        var requested = (req.Roles ?? Array.Empty<string>())
            .Select(r => r?.Trim() ?? "")
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet();

        var invalid = requested.Except(RepairDesk.Core.Auth.AppRoles.All, StringComparer.Ordinal).ToArray();
        if (invalid.Length > 0)
            return BadRequest(new { code = "invalid_roles", invalid });

        // Proteger último Admin do tenant — evitar lock-out acidental.
        if (!requested.Contains(RepairDesk.Core.Auth.AppRoles.Admin)
            && await IsLastAdminInTenantAsync(user))
        {
            return BadRequest(new { code = "last_admin_protected", message = "Não podes remover o último Admin do tenant." });
        }

        var current = await _users.GetRolesAsync(user);
        var toRemove = current.Except(requested).ToArray();
        var toAdd = requested.Except(current).ToArray();

        if (toRemove.Length > 0)
        {
            var rm = await _users.RemoveFromRolesAsync(user, toRemove);
            if (!rm.Succeeded)
                return BadRequest(new { code = "role_remove_failed", errors = rm.Errors.Select(e => e.Code).ToArray() });
        }
        if (toAdd.Length > 0)
        {
            var add = await _users.AddToRolesAsync(user, toAdd);
            if (!add.Succeeded)
                return BadRequest(new { code = "role_add_failed", errors = add.Errors.Select(e => e.Code).ToArray() });
        }

        await _audit.LogAsync(
            AuditAction.Update,
            "AppUser",
            user.Id,
            new { action = "set_roles", added = toAdd, removed = toRemove, final = requested.ToArray() },
            user.TenantId,
            _currentUser.UserId,
            ct);

        var final = await _users.GetRolesAsync(user);
        return Ok(new UserRolesResponse(user.Id, final.ToArray()));
    }

    /// <summary>Devolve as roles actuais do user. Útil para a UI de gestão.</summary>
    [HttpGet("{id:guid}/roles")]
    public async Task<ActionResult<UserRolesResponse>> GetRoles(Guid id)
    {
        var user = await FindUserInCurrentTenantAsync(id);
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        return Ok(new UserRolesResponse(user.Id, roles.ToArray()));
    }

    private async Task<bool> IsLastAdminInTenantAsync(AppUser user)
    {
        var current = await _users.GetRolesAsync(user);
        if (!current.Contains(RepairDesk.Core.Auth.AppRoles.Admin)) return false;
        var admins = await _users.GetUsersInRoleAsync(RepairDesk.Core.Auth.AppRoles.Admin);
        var sameTenantAdmins = admins.Where(u => u.TenantId == user.TenantId).ToArray();
        return sameTenantAdmins.Length == 1 && sameTenantAdmins[0].Id == user.Id;
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
public sealed record SetUserRolesRequest(string[]? Roles);
public sealed record UserRolesResponse(Guid UserId, string[] Roles);
