using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 344 (Doc 83 Pillar 3): captura e listagem de assinaturas digitais
/// recolhidas em tablet/canvas. Ligadas a uma reparação.
/// </summary>
[ApiController]
[Route("api/reparacoes/{reparacaoId:guid}/signatures")]
[Authorize]
public sealed class SignaturesController : ControllerBase
{
    private readonly ISignatureRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public SignaturesController(
        ISignatureRepository repo,
        IReparacaoRepository reparacoes,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public sealed record SignatureDto(
        Guid Id,
        Guid ReparacaoId,
        SignatureType Tipo,
        string ImagemDataUrl,
        string AssinanteNome,
        string? AssinanteContacto,
        DateTime SignedAt);

    public sealed record CaptureRequest(
        SignatureType Tipo,
        string ImagemDataUrl,
        string AssinanteNome,
        string? AssinanteContacto);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SignatureDto>>> List(Guid reparacaoId, CancellationToken ct)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct);
        if (rep is null) return NotFound();
        var list = await _repo.ListByReparacaoAsync(reparacaoId, ct);
        return Ok(list.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SignatureDto>> Capture(Guid reparacaoId, [FromBody] CaptureRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.ImagemDataUrl) || !req.ImagemDataUrl.StartsWith("data:image/", StringComparison.Ordinal))
            return BadRequest(new { code = "invalid_image", message = "ImagemDataUrl tem de ser data URL image/png." });
        if (string.IsNullOrWhiteSpace(req.AssinanteNome))
            return BadRequest(new { code = "missing_name", message = "Nome do assinante obrigatório." });
        // Limite ~2MB para evitar abuse — uma assinatura típica é 20-100KB.
        if (req.ImagemDataUrl.Length > 2_800_000)
            return BadRequest(new { code = "image_too_large", message = "Imagem demasiado grande (max ~2MB base64)." });

        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct);
        if (rep is null) return NotFound();

        var sig = new SignatureCapture
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReparacaoId = reparacaoId,
            Tipo = req.Tipo,
            ImagemDataUrl = req.ImagemDataUrl,
            AssinanteNome = req.AssinanteNome.Trim(),
            AssinanteContacto = string.IsNullOrWhiteSpace(req.AssinanteContacto) ? null : req.AssinanteContacto.Trim(),
            SignedAt = DateTime.UtcNow,
            RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CapturedByUserId = _user.UserId,
        };

        await _repo.AddAsync(sig, ct);
        await _audit.LogAsync(
            AuditAction.Create,
            "SignatureCapture",
            sig.Id,
            new { reparacaoId, tipo = req.Tipo.ToString(), assinanteNome = sig.AssinanteNome },
            tenantId,
            _user.UserId,
            ct);

        return Ok(Map(sig));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid reparacaoId, Guid id, CancellationToken ct)
    {
        var sig = await _repo.FindByIdAsync(id, ct);
        if (sig is null || sig.ReparacaoId != reparacaoId) return NotFound();
        await _repo.DeleteAsync(sig, ct);
        await _audit.LogAsync(
            AuditAction.Delete,
            "SignatureCapture",
            sig.Id,
            new { reparacaoId, tipo = sig.Tipo.ToString() },
            sig.TenantId,
            _user.UserId,
            ct);
        return NoContent();
    }

    private static SignatureDto Map(SignatureCapture s) => new(
        s.Id, s.ReparacaoId, s.Tipo, s.ImagemDataUrl, s.AssinanteNome, s.AssinanteContacto, s.SignedAt);
}
