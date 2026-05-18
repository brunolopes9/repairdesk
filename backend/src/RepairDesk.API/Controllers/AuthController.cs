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
    private readonly ILogger<AuthController> _log;

    public AuthController(
        UserManager<AppUser> users,
        ITokenService tokens,
        IRefreshTokenService refresh,
        IAuditLogger audit,
        ILogger<AuthController> log)
    {
        _users = users;
        _tokens = tokens;
        _refresh = refresh;
        _audit = audit;
        _log = log;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(new { code = "invalid_credentials" });

        if (await _users.IsLockedOutAsync(user))
            return Unauthorized(new { code = "locked_out" });

        var passwordOk = await _users.CheckPasswordAsync(user, req.Password);
        if (!passwordOk)
        {
            await _users.AccessFailedAsync(user);
            _log.LogWarning("Failed login for {Email} from {Ip}", req.Email, ip);
            return Unauthorized(new
            {
                code = await _users.IsLockedOutAsync(user) ? "locked_out" : "invalid_credentials"
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
        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await _users.GetRolesAsync(user);
        return Ok(new UserInfo(user.Id, user.Email!, user.DisplayName, user.TenantId, roles.ToList()));
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(AppUser user, string? ip, CancellationToken ct)
    {
        var (plaintext, _) = await _refresh.IssueAsync(user, ip, ct);
        return Ok(await BuildResponse(user, plaintext));
    }

    private async Task<AuthResponse> BuildResponse(AppUser user, string refreshPlaintext)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = _tokens.IssueAccessToken(user, roles);

        Response.Cookies.Append(RefreshCookieName, refreshPlaintext, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return new AuthResponse(
            access,
            _tokens.AccessTokenExpiry,
            new UserInfo(user.Id, user.Email!, user.DisplayName, user.TenantId, roles.ToList()));
    }
}
