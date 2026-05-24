using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.Services.Garantias;
using RepairDesk.Services.Push;
using RepairDesk.Services.PublicPortal;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Portal cliente público. **Sem autenticação**. Segurança via slug
/// não-adivinhável + rate limiting.
/// </summary>
[ApiController]
[Route("api/public/repair")]
[AllowAnonymous]
[EnableRateLimiting("public-portal")]
public class PublicPortalController : ControllerBase
{
    private readonly IPublicPortalService _service;

    public PublicPortalController(IPublicPortalService service) => _service = service;

    // Sprint 252 (Doc 75 área 9 P2): output cache 30s no GET portal. Cliente
    // que recarrega a página (ou app a fazer polling) bate uma vez por 30s.
    // Slug-scoped — caches separados por reparação. Em prod com 100 clientes a
    // refrescar a cada 10s, isto poupa ~70% das queries DB.
    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "public-portal-30s")]
    public Task<PublicRepairDto> Get(string slug, CancellationToken ct)
        => _service.GetBySlugAsync(slug, ct);

    [HttpPost("{slug}/orcamento")]
    public Task<PublicRepairDto> AprovarOrcamento(string slug, [FromBody] AprovarOrcamentoRequest req, CancellationToken ct)
        => _service.AprovarOrcamentoAsync(slug, req.Aceitar, ct);

    [HttpPost("{slug}/avaliacao")]
    public Task<AvaliacaoSubmittedDto> SubmeterAvaliacao(string slug, [FromBody] SubmitAvaliacaoRequest req, CancellationToken ct)
        => _service.SubmeterAvaliacaoAsync(slug, req.Score, req.Comentario, req.PublicarTestemunho, ct);
}

/// <summary>
/// Endpoint público de verificação de garantia. Sem auth, rate-limited.
/// </summary>
[ApiController]
[Route("api/public/warranty")]
[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-portal")]
public class PublicWarrantyController : ControllerBase
{
    private readonly IPublicPortalService _service;
    private readonly IGarantiaService _garantias;
    public PublicWarrantyController(IPublicPortalService service, IGarantiaService garantias)
    {
        _service = service;
        _garantias = garantias;
    }

    // Garantia muda raramente; cache 5min é seguro. Anular garantia invalida via
    // tag (futuro) ou via TTL natural.
    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "public-warranty-5min")]
    public Task<PublicGarantiaDto> Get(string slug, CancellationToken ct)
        => _service.GetGarantiaBySlugAsync(slug, ct);

    /// <summary>
    /// PDF da garantia, descarregável sem login a partir do portal /g/{slug}.
    /// Slug não-adivinhável funciona como token de acesso. 404 se slug não existe.
    /// </summary>
    [HttpGet("{slug}/pdf")]
    public async Task<IActionResult> Pdf(string slug, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _garantias.RenderPdfBySlugAsync(slug, baseUrl, ct);
        return result is null ? NotFound() : File(result.Value.Pdf, "application/pdf", result.Value.Filename);
    }
}

/// <summary>
/// Subscrições Web Push do portal público. Sem autenticação; o slug não-adivinhável
/// limita o âmbito à reparação do cliente.
/// </summary>
[ApiController]
[Route("api/public/portal")]
[AllowAnonymous]
[EnableRateLimiting("public-portal")]
public class PublicPortalPushController : ControllerBase
{
    private readonly IPushNotificationService _push;

    public PublicPortalPushController(IPushNotificationService push) => _push = push;

    [HttpGet("push/vapid-public-key")]
    public Task<VapidPublicKeyDto> GetVapidPublicKey(CancellationToken ct)
        => _push.GetVapidPublicKeyAsync(ct);

    [HttpPost("{slug}/push/subscribe")]
    public Task<PushSubscriptionResultDto> Subscribe(string slug, [FromBody] BrowserPushSubscriptionDto request, CancellationToken ct)
        => _push.SubscribeAsync(slug, request, ct);

    [HttpDelete("{slug}/push/unsubscribe")]
    public Task<PushSubscriptionResultDto> Unsubscribe(string slug, [FromBody] UnsubscribePushRequest request, CancellationToken ct)
        => _push.UnsubscribeAsync(slug, request, ct);
}
