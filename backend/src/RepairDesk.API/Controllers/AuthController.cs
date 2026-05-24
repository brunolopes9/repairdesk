using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public const string RefreshCookieName = "rd_refresh";

    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refresh;
    private readonly IAuditLogger _audit;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthController> _log;

    public AuthController(
        UserManager<AppUser> users,
        ITokenService tokens,
        IRefreshTokenService refresh,
        IAuditLogger audit,
        IHostEnvironment env,
        ILogger<AuthController> log)
    {
        _users = users;
        _tokens = tokens;
        _refresh = refresh;
        _audit = audit;
        _env = env;
        _log = log;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-strict")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
        {
            await LogFailedLoginAsync(req, null, "invalid_credentials", ip, ct);
            return Unauthorized(new { code = "invalid_credentials" });
        }

        if (!user.IsActive)
        {
            await LogFailedLoginAsync(req, user, "user_inactive", ip, ct);
            return Unauthorized(new { code = "invalid_credentials" });
        }

        if (await _users.IsLockedOutAsync(user))
        {
            await LogFailedLoginAsync(req, user, "locked_out", ip, ct);
            return Unauthorized(new { code = "locked_out" });
        }

        var passwordOk = await _users.CheckPasswordAsync(user, req.Password);
        if (!passwordOk)
        {
            await _users.AccessFailedAsync(user);
            _log.LogWarning("Failed login for {Email} from {Ip}", req.Email, ip);
            var code = await _users.IsLockedOutAsync(user) ? "locked_out" : "invalid_credentials";
            await LogFailedLoginAsync(req, user, code, ip, ct);
            return Unauthorized(new
            {
                code
            });
        }

        await _users.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ip;
        await _users.UpdateAsync(user);
        await _audit.LogAsync(AuditAction.Login, "AppUser", user.Id, new { email = user.Email }, user.TenantId, user.Id, ct);

        return await IssueTokensAsync(user, ip, ct);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var plaintext) || string.IsNullOrEmpty(plaintext))
            return Unauthorized(new { code = "missing_refresh" });

        var token = await _refresh.ValidateAsync(plaintext, ct);
        if (token is null) return Unauthorized(new { code = "invalid_refresh" });

        var user = await _users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive) return Unauthorized(new { code = "user_inactive" });

        var (newPlaintext, _) = await _refresh.RotateAsync(token, user, ip, ct);
        return Ok(await BuildResponse(user, newPlaintext));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var plaintext) && !string.IsNullOrEmpty(plaintext))
        {
            var token = await _refresh.ValidateAsync(plaintext, ct);
            if (token is not null)
                await _refresh.RevokeAsync(token, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        }
        var user = await _users.GetUserAsync(User);
        if (user is not null)
            await _audit.LogAsync(AuditAction.Logout, "AppUser", user.Id, new { email = user.Email }, user.TenantId, user.Id, ct);

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null || !user.IsActive) return Unauthorized(new { code = "user_inactive" });

        var result = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                code = "password_change_failed",
                errors = result.Errors.Select(e => e.Code).ToList()
            });
        }

        user.RequireChangePasswordOnNextLogin = false;
        user.LastLoginAt = DateTime.UtcNow;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        user.LastLoginIp = ip;
        await _users.UpdateAsync(user);
        await _audit.LogAsync(
            AuditAction.Update,
            "AppUser",
            user.Id,
            new { requireChangePasswordOnNextLogin = false },
            user.TenantId,
            user.Id,
            ct);

        await _refresh.RevokeAllForUserAsync(user.Id, ip, ct);
        DeleteRefreshCookie();
        return Ok(await BuildAccessResponse(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await _users.GetRolesAsync(user);
        return Ok(ToUserInfo(user, roles));
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(AppUser user, string? ip, CancellationToken ct)
    {
        var (plaintext, _) = await _refresh.IssueAsync(user, ip, ct);
        return Ok(await BuildResponse(user, plaintext));
    }

    private Task LogFailedLoginAsync(LoginRequest req, AppUser? user, string reason, string? ip, CancellationToken ct)
        => _audit.LogAsync(
            AuditAction.LoginFailed,
            "Auth",
            user?.Id,
            new { email = req.Email, ip, reason },
            user?.TenantId ?? Guid.Empty,
            user?.Id,
            ct);

    private async Task<AuthResponse> BuildResponse(AppUser user, string refreshPlaintext)
    {
        Response.Cookies.Append(RefreshCookieName, refreshPlaintext, BuildRefreshCookieOptions(DateTimeOffset.UtcNow.AddDays(7)));
        return await BuildAccessResponse(user);
    }

    private async Task<AuthResponse> BuildAccessResponse(AppUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = _tokens.IssueAccessToken(user, roles);

        return new AuthResponse(access, _tokens.AccessTokenExpiry, ToUserInfo(user, roles));
    }

    private CookieOptions BuildRefreshCookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = string.Equals(Request.Scheme, "https", StringComparison.OrdinalIgnoreCase),
        SameSite = _env.IsProduction() ? SameSiteMode.Strict : SameSiteMode.Lax,
        Path = "/api/auth",
        Expires = expires
    };

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions());
    }

    private static UserInfo ToUserInfo(AppUser user, IEnumerable<string> roles)
        => new(user.Id, user.Email!, user.DisplayName, user.TenantId, roles.ToList(), user.RequireChangePasswordOnNextLogin);
}
