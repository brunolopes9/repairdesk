using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    [HttpGet("{slug}")]
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
    public PublicWarrantyController(IPublicPortalService service) => _service = service;

    [HttpGet("{slug}")]
    public Task<PublicGarantiaDto> Get(string slug, CancellationToken ct)
        => _service.GetGarantiaBySlugAsync(slug, ct);
}
