using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.HostedServices;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

public sealed record FaturasEmailConfigDto(bool Configurado, string? Host, int? Port, string? User, DateTime? UltimaLeitura, string? UltimoErro);
public sealed record GuardarFaturasEmailRequest(string Host, int? Port, string User, string Password);

/// <summary>
/// Sprint 620 (Doc 98 F5): ligar a caixa dedicada às faturas (IMAP). Só o dono. A ligação é testada antes de guardar;
/// a palavra-passe fica cifrada e nunca é devolvida.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/faturas-email")]
public sealed class FaturasEmailController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly FaturasImapLeitor _leitor;

    public FaturasEmailController(AppDbContext db, ITenantContext tenant, ISecretProtector secrets, FaturasImapLeitor leitor)
    {
        _db = db;
        _tenant = tenant;
        _secrets = secrets;
        _leitor = leitor;
    }

    private async Task<RepairDesk.Core.Entities.Tenant> TenantAsync(CancellationToken ct)
        => (_tenant.TenantId is { } id ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct) : null)
           ?? throw new ForbiddenException("tenant_required", "Sem empresa no contexto.");

    private static FaturasEmailConfigDto Dto(RepairDesk.Core.Entities.Tenant t) => new(
        t.FaturasImapHost is not null && t.FaturasImapPasswordCifrada is not null,
        t.FaturasImapHost, t.FaturasImapPort, t.FaturasImapUser, t.FaturasImapUltimaLeitura, t.FaturasImapUltimoErro);

    [HttpGet]
    public async Task<ActionResult<FaturasEmailConfigDto>> Get(CancellationToken ct) => Ok(Dto(await TenantAsync(ct)));

    [HttpPut]
    public async Task<ActionResult<FaturasEmailConfigDto>> Guardar([FromBody] GuardarFaturasEmailRequest req, CancellationToken ct)
    {
        var host = req.Host?.Trim().ToLowerInvariant();
        var user = req.User?.Trim();
        var port = req.Port ?? 993;
        if (!FaturasEmail.HostValido(host)) throw new ValidationException("host_invalido", "Servidor IMAP inválido (ex.: mail.lopestech.pt).");
        if (port is not (993 or 143)) throw new ValidationException("porta_invalida", "Porta IMAP: 993 (recomendado).");
        if (string.IsNullOrEmpty(user) || user.Length > 200) throw new ValidationException("user_invalido", "Indica o endereço da caixa (ex.: faturas@lopestech.pt).");
        if (string.IsNullOrEmpty(req.Password) || req.Password.Length > 500) throw new ValidationException("password_invalida", "Indica a palavra-passe da caixa.");
        try
        {
            await FaturasImapLeitor.TestarAsync(host!, port, user, req.Password, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ValidationException("ligacao_falhou", ex is MailKit.Security.AuthenticationException
                ? "O servidor recusou o utilizador ou a palavra-passe."
                : "Não consegui ligar ao servidor IMAP (confirma o endereço e a porta 993).");
        }
        var t = await TenantAsync(ct);
        t.FaturasImapHost = host;
        t.FaturasImapPort = port;
        t.FaturasImapUser = user;
        t.FaturasImapPasswordCifrada = _secrets.Protect(req.Password);
        t.FaturasImapUltimoErro = null;
        await _db.SaveChangesAsync(ct);
        return Ok(Dto(t));
    }

    [HttpDelete]
    public async Task<IActionResult> Desligar(CancellationToken ct)
    {
        var t = await TenantAsync(ct);
        t.FaturasImapHost = null;
        t.FaturasImapPort = null;
        t.FaturasImapUser = null;
        t.FaturasImapPasswordCifrada = null;
        t.FaturasImapUltimoErro = null;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Lê a caixa já (sem esperar pelos 5 minutos).</summary>
    [HttpPost("ler-agora")]
    public async Task<ActionResult<object>> LerAgora(CancellationToken ct)
    {
        var t = await TenantAsync(ct);
        var n = await _leitor.LerAsync(t.Id, ct);
        await _db.Entry(t).ReloadAsync(ct);
        return Ok(new { importados = n, config = Dto(t) });
    }
}
