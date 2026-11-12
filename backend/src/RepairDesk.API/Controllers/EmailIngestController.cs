using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 173: webhook que recebe emails forwarded para faturas-{slug}@ingest.repairdesk.app.
/// Implementação compatível com Cloudflare Email Routing (grátis ilimitado), Mailgun, Postmark.
///
/// Fluxo:
///   1. Tenant configura forward Gmail "from:utopya@*" → faturas-lopestech@ingest.repairdesk.app
///   2. Cloudflare Email Routing recebe → POST a este endpoint
///   3. Detectamos tenant pelo TO header → executamos pipeline ingest
///
/// RGPD: tenant escolheu activamente forward — consentimento implícito. Não temos credenciais
/// IMAP, scope limitado aos emails que tenant configurou forward.
/// </summary>
[ApiController]
[Route("api/external/email-ingest")]
public class EmailIngestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISupplierInvoiceImportService _ingestService;
    private readonly ILogger<EmailIngestController> _logger;
    private readonly IConfiguration _config;

    public EmailIngestController(
        AppDbContext db,
        ISupplierInvoiceImportService ingestService,
        IConfiguration config,
        ILogger<EmailIngestController> logger)
    {
        _db = db;
        _ingestService = ingestService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Webhook payload genérico — Cloudflare Email Routing, Mailgun ou Postmark fazem POST aqui.
    /// Aceita base64-encoded attachments (PDF, JPG).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]  // Auth via shared secret no header (não JWT)
    public async Task<IActionResult> Receive([FromBody] EmailWebhookPayload payload, CancellationToken ct)
    {
        // Shared secret na header — só Cloudflare/Mailgun deve saber.
        var expected = _config["EmailIngest:SharedSecret"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            _logger.LogError("EmailIngest:SharedSecret não configurado — rejeitando webhook.");
            return Unauthorized();
        }
        if (!Request.Headers.TryGetValue("X-Webhook-Secret", out var providedSecret)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(providedSecret.ToString()),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            _logger.LogWarning("EmailIngest webhook recebido com secret inválido from {Ip}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        // Detecta tenant pelo TO header — esperamos "faturas-{slug}@ingest.repairdesk.app".
        var slug = ExtractSlugFromEmail(payload.To);
        if (slug is null)
        {
            _logger.LogWarning("EmailIngest: TO '{To}' não match formato faturas-*@ingest.repairdesk.app", payload.To);
            return BadRequest(new { code = "invalid_to", to = payload.To });
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IngestEmailSlug == slug, ct);
        if (tenant is null)
        {
            _logger.LogWarning("EmailIngest: slug '{Slug}' não corresponde a tenant", slug);
            return NotFound(new { code = "tenant_not_found" });
        }

        if (payload.Attachments is null || payload.Attachments.Count == 0)
        {
            _logger.LogInformation("EmailIngest: email para {Slug} sem anexos — ignorado", slug);
            return Ok(new { ignored = true, reason = "no_attachments" });
        }

        // Processa cada anexo PDF/imagem como ingest separado.
        var results = new List<object>();
        foreach (var att in payload.Attachments)
        {
            if (string.IsNullOrWhiteSpace(att.Content)) continue;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(att.Content); }
            catch { _logger.LogWarning("EmailIngest: anexo {Name} base64 inválido", att.Name); continue; }
            if (bytes.Length == 0) continue;

            var meta = new SupplierInvoiceEmailMeta(
                MessageId: payload.MessageId,
                Subject: payload.Subject,
                From: payload.From,
                ReceivedAt: payload.ReceivedAt ?? DateTime.UtcNow);

            try
            {
                // Sprint 173: overload com tenantId explícito — webhook é anonymous, sem HttpContext.User claim.
                var result = await _ingestService.IngestAsExternalAsync(
                    tenantId: tenant.Id,
                    pdfBytes: bytes,
                    originalFilename: att.Name,
                    emailMeta: meta,
                    apiKeyId: null,
                    ct: ct);
                results.Add(new { name = att.Name, importId = result.ImportId, duplicate = result.WasDuplicate, status = result.Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailIngest: anexo {Name} falhou", att.Name);
                results.Add(new { name = att.Name, error = ex.Message });
            }
        }

        return Ok(new { processed = results.Count, results });
    }

    /// <summary>Extrai "lopestech" de "faturas-lopestech@ingest.repairdesk.app".</summary>
    /// <summary>
    /// Sprint 632 (Doc 98 F5): o Email Worker da Cloudflare (ex.: faturas@lopestech.pt) envia o email inteiro (MIME).
    /// A empresa é a que tem esse endereço; o segredo (X-Webhook-Secret) tem de bater com o hash guardado.
    /// Resposta: <c>semPdf=true</c> → o Worker reencaminha o email para o Gmail do dono (ex.: código de confirmação).
    /// </summary>
    [HttpPost("mime")]
    [AllowAnonymous]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [Consumes("message/rfc822", "application/octet-stream")]
    public async Task<IActionResult> ReceiveMime([FromServices] HostedServices.FaturasImapLeitor leitor,
        [FromServices] IMemoryCache cache, CancellationToken ct)
    {
        var para = Request.Headers["X-Para"].ToString().Trim().ToLowerInvariant();
        var segredo = Request.Headers["X-Webhook-Secret"].ToString();
        if (string.IsNullOrEmpty(para) || string.IsNullOrEmpty(segredo)) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FaturasEmailEndereco == para && !t.IsDeleted, ct);
        if (tenant?.FaturasEmailSegredoHash is not { } hash
            || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(FaturasEmailSegredo.Hash(segredo)), System.Text.Encoding.ASCII.GetBytes(hash)))
        {
            _logger.LogWarning("EmailIngest MIME: segredo/endereço inválido para {Para} de {Ip}", para, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }
        MimeKit.MimeMessage msg;
        try { msg = await MimeKit.MimeMessage.LoadAsync(Request.Body, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "EmailIngest MIME: email ilegível");
            return BadRequest(new { code = "mime_invalido" });
        }
        var (pdfs, novos, falhas) = await leitor.ImportarEmailAsync(tenant.Id, msg, ct);
        tenant.FaturasEmailUltimoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await leitor.AvisarNovasAsync(tenant.Id, novos, ct);
        _logger.LogInformation("EmailIngest MIME: «{Assunto}» → {Pdfs} PDF(s), {Novos} novo(s), {Falhas} falha(s)", msg.Subject, pdfs, novos, falhas);

        // Sprint 639: o Worker devolve ao Gmail o que falha; se uma regra de reencaminhar do Gmail/Hotmail
        // o mandar outra vez para aqui, não devolver de novo — senão um PDF estragado andava em círculo.
        var devolver = pdfs == 0 || falhas > 0;
        if (devolver && !string.IsNullOrEmpty(msg.MessageId))
        {
            var chave = $"faturas-email-devolvido:{tenant.Id:N}:{msg.MessageId}";
            if (cache.TryGetValue(chave, out _))
            {
                _logger.LogWarning("EmailIngest MIME: «{Assunto}» voltou depois de devolvido — não devolvo outra vez.", msg.Subject);
                return Ok(new { pdfs, novos, falhas = 0, semPdf = false, repetido = true });
            }
            cache.Set(chave, true, TimeSpan.FromDays(3));
        }
        return Ok(new { pdfs, novos, falhas, semPdf = pdfs == 0 });
    }

    internal static string? ExtractSlugFromEmail(string to)
    {
        if (string.IsNullOrWhiteSpace(to)) return null;
        var lower = to.Trim().ToLowerInvariant();
        var match = System.Text.RegularExpressions.Regex.Match(lower, @"^faturas-([a-z0-9\-]+)@");
        return match.Success ? match.Groups[1].Value : null;
    }
}

public sealed record EmailWebhookPayload(
    string To,
    string From,
    string? Subject,
    string? MessageId,
    DateTime? ReceivedAt,
    List<EmailAttachment>? Attachments);

public sealed record EmailAttachment(string Name, string ContentType, string Content);

/// <summary>Sprint 632: segredo do Email Worker — só o hash fica guardado.</summary>
public static class FaturasEmailSegredo
{
    public static string Gerar() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string segredo) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(segredo))).ToLowerInvariant();
}
