using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 606: a mesma pessoa trabalha em várias empresas (LopesTech, Cortex Automation) com a mesma
/// conta. Trocar de empresa = nova sessão (refresh + access token) com essa empresa como tenant ativo;
/// os dados de cada empresa continuam separados pelos filtros de tenant de sempre.
/// </summary>
[ApiController]
[Authorize]
[Route("api/auth/empresas")]
public sealed class EmpresaAtivaController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refresh;
    private readonly AcessosEmpresaService _acessos;
    private readonly IAuditLogger _audit;
    private readonly IHostEnvironment _env;

    public EmpresaAtivaController(UserManager<AppUser> users, ITokenService tokens, IRefreshTokenService refresh,
        AcessosEmpresaService acessos, IAuditLogger audit, IHostEnvironment env)
    {
        _users = users;
        _tokens = tokens;
        _refresh = refresh;
        _acessos = acessos;
        _audit = audit;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmpresaDisponivelDto>>> Listar(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await _acessos.ListarAsync(user, Ativa() ?? user.TenantId, ct));
    }

    [HttpPost("{tenantId:guid}/ativar")]
    public async Task<ActionResult<AuthResponse>> Ativar(Guid tenantId, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null || !user.IsActive) return Unauthorized(new { code = "user_inactive" });
        if (!await _acessos.PodeAsync(user, tenantId, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "sem_acesso_empresa", detail = "Não tens acesso a essa empresa." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (Request.Cookies.TryGetValue(AuthController.RefreshCookieName, out var atual) && !string.IsNullOrEmpty(atual)
            && await _refresh.ValidateAsync(atual, ct) is { } antigo)
            await _refresh.RevokeAsync(antigo, ip, ct);

        var (plaintext, _) = await _refresh.IssueParaEmpresaAsync(user, tenantId, ip, ct);
        Response.Cookies.Append(AuthController.RefreshCookieName, plaintext,
            AuthController.RefreshCookieOptions(Request, _env.IsProduction(), DateTimeOffset.UtcNow.AddDays(7)));

        var roles = await _users.GetRolesAsync(user);
        var access = tenantId == user.TenantId ? _tokens.IssueAccessToken(user, roles) : _tokens.IssueAccessToken(user, roles, tenantId);
        await _audit.LogAsync(AuditAction.Login, "Empresa", tenantId, new { trocouDe = Ativa(), para = tenantId }, tenantId, user.Id, ct);
        return Ok(new AuthResponse(access, _tokens.AccessTokenExpiry, AuthController.ToUserInfo(user, roles, tenantId)));
    }

    private Guid? Ativa() => Guid.TryParse(User.FindFirst(HttpTenantContext.TenantIdClaim)?.Value, out var t) ? t : null;
}
