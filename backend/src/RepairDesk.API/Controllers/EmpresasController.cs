using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Auth;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

public sealed record NovaEmpresaRequest(string Nome, string Email);
public sealed record NovaEmpresaDto(Guid TenantId, string Nome, string Email);

/// <summary>
/// Sprint 604: o dono da plataforma (administrador da LopesTech) cria uma empresa nova — por
/// agora a Cortex Automation. O Mender não tem registo público: cada empresa nasce aqui, com um
/// administrador próprio (email único) que escolhe a palavra-passe pelo link enviado por email.
/// A palavra-passe nunca passa por quem cria a empresa.
/// </summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/plataforma/empresas")]
public sealed class EmpresasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly ILookupNormalizer _normalizer;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<EmpresasController> _log;

    public EmpresasController(AppDbContext db, UserManager<AppUser> users, ILookupNormalizer normalizer, ITenantContext tenant,
        IEmailSender email, IAuditLogger audit, IConfiguration config, ILogger<EmpresasController> log)
    {
        _db = db;
        _users = users;
        _normalizer = normalizer;
        _tenant = tenant;
        _email = email;
        _audit = audit;
        _config = config;
        _log = log;
    }

    [HttpPost]
    public async Task<ActionResult<NovaEmpresaDto>> Criar([FromBody] NovaEmpresaRequest req, CancellationToken ct)
    {
        // O dono da plataforma é o tenant da LopesTech (configurável para testes: Plataforma:TenantDono).
        var dono = Guid.TryParse(_config["Plataforma:TenantDono"], out var d) ? d : DbInitializer.LopesTechTenantId;
        if (_tenant.TenantId != dono)
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "so_dono_plataforma", detail = "Só o dono da plataforma cria empresas." });

        var nome = req.Nome?.Trim() ?? "";
        var email = req.Email?.Trim() ?? "";
        if (nome.Length is < 2 or > 120)
            return UnprocessableEntity(new { code = "nome_invalido", detail = "O nome da empresa tem de ter entre 2 e 120 caracteres." });
        if (!MailAddress.TryCreate(email, out _))
            return UnprocessableEntity(new { code = "email_invalido", detail = "Email inválido." });

        // O email identifica a conta em todas as empresas: procura-se sem o filtro de tenant.
        var normalizado = _normalizer.NormalizeEmail(email);
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizado, ct))
            return Conflict(new { code = "email_em_uso", detail = "Já existe uma conta com este email. Usa outro (ex.: nome+empresa@gmail.com)." });

        var quem = await _users.GetUserAsync(User);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = nome, Email = email, IsActive = true };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        var admin = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = quem?.DisplayName ?? nome,
            IsActive = true,
        };
        var criado = await _users.CreateAsync(admin);
        if (!criado.Succeeded)
        {
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync(ct);
            return UnprocessableEntity(new { code = "conta_invalida", detail = string.Join(" ", criado.Errors.Select(e => e.Description)) });
        }
        await _users.AddToRoleAsync(admin, AppRoles.Admin);
        await _audit.LogAsync(AuditAction.Create, "Tenant", tenant.Id, new { nome, email, criadoPor = quem?.Id }, tenant.Id, quem?.Id, ct);

        try
        {
            await _email.SendAsync(BoasVindas(admin, nome, await LinkPassword(admin)), ct);
        }
        catch (Exception ex)
        {
            // A empresa fica criada; a pessoa pode usar «Esqueci a palavra-passe» com o mesmo email.
            _log.LogError(ex, "Falha a enviar o email de boas-vindas da empresa {TenantId}", tenant.Id);
        }

        return Created($"/api/plataforma/empresas/{tenant.Id}", new NovaEmpresaDto(tenant.Id, nome, email));
    }

    private async Task<string> LinkPassword(AppUser user)
    {
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var codificado = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = (_config["Frontend:BaseUrl"] ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/auth/reset-password?uid={user.Id}&token={codificado}";
    }

    private static EmailMessage BoasVindas(AppUser user, string empresa, string link)
    {
        var e = WebUtility.HtmlEncode(empresa);
        var l = WebUtility.HtmlEncode(link);
        var html = $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:480px;margin:0 auto;color:#1f2937">
              <h2 style="margin:0 0 16px">{e} já está no Mender</h2>
              <p>Esta é a conta de administrador da empresa <b>{e}</b>. Escolhe a palavra-passe para entrar (o link vale 1 hora; depois disso usa «Esqueci a palavra-passe» com este email).</p>
              <p style="margin:24px 0"><a href="{l}" style="background:#111827;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;display:inline-block">Escolher palavra-passe</a></p>
            </div>
            """;
        var text = $"{empresa} já está no Mender.\n\nEscolhe a palavra-passe (link válido 1 hora):\n{link}\n";
        return new EmailMessage(user.Email!, $"{empresa} — escolhe a palavra-passe do Mender", html, text);
    }
}
