using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

/// <summary>
/// "Esqueci a palavra-passe": envia um link de reset (válido 1h) para o email da conta e
/// aplica a nova palavra-passe. Nunca revela se a conta existe.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class PasswordResetController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly IRefreshTokenService _refresh;
    private readonly IEmailSender _email;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<PasswordResetController> _log;

    public PasswordResetController(
        UserManager<AppUser> users,
        IRefreshTokenService refresh,
        IEmailSender email,
        IAuditLogger audit,
        IConfiguration config,
        ILogger<PasswordResetController> log)
    {
        _users = users;
        _refresh = refresh;
        _email = email;
        _audit = audit;
        _config = config;
        _log = log;
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-reset")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var user = await _users.FindByLoginAsync(req.Login);
        if (user is { IsActive: true } && !string.IsNullOrWhiteSpace(user.Email))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = (_config["Frontend:BaseUrl"] ?? string.Empty).TrimEnd('/');
            var link = $"{baseUrl}/auth/reset-password?uid={user.Id}&token={encodedToken}";

            try
            {
                await _email.SendAsync(PasswordResetEmail.Build(user.Email, user.DisplayName, link), ct);
                await _audit.LogAsync(AuditAction.Update, "AppUser", user.Id, new { passwordResetRequested = true }, user.TenantId, user.Id, ct);
            }
            catch (Exception ex)
            {
                // O utilizador recebe a mesma resposta; o erro fica no log/Sentry.
                _log.LogError(ex, "Falha a enviar email de reset para o utilizador {UserId}", user.Id);
            }
        }

        return Accepted(new { code = "reset_requested" });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(req.UserId.ToString());
        if (user is null || !user.IsActive)
            return BadRequest(new { code = "invalid_token" });

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
        }
        catch (FormatException)
        {
            return BadRequest(new { code = "invalid_token" });
        }

        var result = await _users.ResetPasswordAsync(user, token, req.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Code).ToList();
            var code = errors.Contains("InvalidToken") ? "invalid_token" : "password_invalid";
            return BadRequest(new { code, errors });
        }

        // Quem fez reset já escolheu uma palavra-passe nova: desbloqueia e termina sessões antigas.
        user.RequireChangePasswordOnNextLogin = false;
        await _users.UpdateAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _refresh.RevokeAllForUserAsync(user.Id, ip, ct);
        await _audit.LogAsync(AuditAction.Update, "AppUser", user.Id, new { passwordReset = true, ip }, user.TenantId, user.Id, ct);

        return NoContent();
    }
}
