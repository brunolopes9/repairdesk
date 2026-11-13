using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 656: checkout e seguimento da loja do lopestech.pt — **sem autenticação**. A empresa vem do IntakeSlug
/// (o mesmo do formulário de pedidos de reparação); a partir daí tudo corre dentro dessa empresa (filtros normais).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/loja")]
public sealed class PublicLojaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEncomendaLojaService _service;
    private readonly IEncomendaLojaRepository _repo;
    private readonly StripeOptions _stripe;
    private readonly ILogger<PublicLojaController> _log;

    public PublicLojaController(AppDbContext db, IEncomendaLojaService service, IEncomendaLojaRepository repo,
        IOptions<StripeOptions> stripe, ILogger<PublicLojaController> log)
    {
        _db = db;
        _service = service;
        _repo = repo;
        _stripe = stripe.Value;
        _log = log;
    }

    [HttpPost("{intakeSlug}/checkout")]
    [EnableRateLimiting("public-portal")]
    public async Task<ActionResult<CheckoutResultado>> Checkout(string intakeSlug, [FromBody] CheckoutRequest req, CancellationToken ct)
    {
        // Campo-armadilha preenchido = bot. Resposta neutra, nada criado.
        if (!string.IsNullOrWhiteSpace(req.Website)) return BadRequest();
        if (!await EntrarNaEmpresa(intakeSlug, ct)) return NotFound();
        return Ok(await _service.CheckoutAsync(req, ct));
    }

    [HttpGet("{intakeSlug}/encomendas/{referencia}")]
    [EnableRateLimiting("public-portal")]
    public async Task<ActionResult<EncomendaPublicaDto>> Encomenda(string intakeSlug, string referencia, [FromQuery] string t, CancellationToken ct)
    {
        if (!await EntrarNaEmpresa(intakeSlug, ct)) return NotFound();
        return Ok(await _service.EstadoPublicoAsync(referencia, t, ct));
    }

    /// <summary>Eventos do Stripe. Assinatura obrigatória; responde 200 a eventos que não interessam (senão o Stripe repete).</summary>
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body)) payload = await reader.ReadToEndAsync(ct);
        if (!Services.Loja.StripeWebhook.AssinaturaValida(payload, Request.Headers["Stripe-Signature"], _stripe.WebhookSecret, DateTimeOffset.UtcNow))
        {
            _log.LogWarning("Webhook do Stripe com assinatura inválida");
            return BadRequest();
        }

        string? tipo, sessionId, paymentStatus, paymentIntent;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            tipo = doc.RootElement.GetProperty("type").GetString();
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");
            if (obj.TryGetProperty("object", out var o) && o.GetString() != "checkout.session") return Ok();
            sessionId = obj.TryGetProperty("id", out var id) ? id.GetString() : null;
            paymentStatus = obj.TryGetProperty("payment_status", out var ps) ? ps.GetString() : null;
            paymentIntent = obj.TryGetProperty("payment_intent", out var pi) && pi.ValueKind == JsonValueKind.String ? pi.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Webhook do Stripe ilegível");
            return BadRequest();
        }
        if (string.IsNullOrEmpty(sessionId)) return Ok();

        var e = await _repo.FindByStripeSessionGlobalAsync(sessionId, ct);
        if (e is null)
        {
            _log.LogInformation("Webhook do Stripe para sessão desconhecida {Sessao}", sessionId);
            return Ok();
        }
        Entrar(e.TenantId);

        switch (tipo)
        {
            case "checkout.session.completed" when paymentStatus == "paid":
            case "checkout.session.async_payment_succeeded":
                await _service.ConfirmarPagamentoAsync(e, paymentIntent, ct);
                break;
            case "checkout.session.expired":
            case "checkout.session.async_payment_failed":
                await _service.ExpirarAsync(e, ct);
                break;
        }
        return Ok();
    }

    private async Task<bool> EntrarNaEmpresa(string intakeSlug, CancellationToken ct)
    {
        var tenantId = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.IntakeSlug == intakeSlug && t.IsActive).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (tenantId is null) return false;
        Entrar(tenantId.Value);
        return true;
    }

    /// <summary>Identidade mínima do pedido anónimo: só a empresa (para os filtros e serviços correrem dentro dela).</summary>
    private void Entrar(Guid tenantId)
        => HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(HttpTenantContext.TenantIdClaim, tenantId.ToString()) }, authenticationType: null));
}
