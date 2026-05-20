using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.External;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Endpoints para integrações servidor-a-servidor (loja online, kiosks, importadores).
/// Autorizados APENAS via API key — não aceita JWT de utilizador. Tenant resolvido
/// automaticamente pelo claim da API key. Rate-limited a 120 req/min por chave (Sprint 80).
/// </summary>
[ApiController]
[Route("api/external")]
[Authorize(AuthenticationSchemes = ApiKeyAuthHandler.SchemeName)]
[EnableRateLimiting("external-apikey")]
public class ExternalController : ControllerBase
{
    private readonly IExternalCheckoutService _checkout;

    public ExternalController(IExternalCheckoutService checkout) => _checkout = checkout;

    /// <summary>
    /// Health check para integradores. Confirma que a API key é válida, devolve hora do
    /// servidor (para clock skew) e o tenantId resolvido. Resposta tem de ser O(1) — não
    /// toca BD além do que o ApiKeyAuthHandler já fez.
    /// </summary>
    [HttpGet("health")]
    public ActionResult<ExternalHealthResponse> Health()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        return Ok(new ExternalHealthResponse(
            Status: "ok",
            ServerTime: DateTimeOffset.UtcNow,
            ApiVersion: "1.0",
            TenantId: tenantClaim is null ? null : Guid.Parse(tenantClaim)));
    }

    /// <summary>
    /// Fecha uma venda inteira atomicamente. Use isto a partir de loja online (depois do pagamento
    /// ter sido confirmado via Stripe/EuPago) em vez de fazer 3 chamadas separadas.
    /// </summary>
    [HttpPost("checkout")]
    [ApiScope("write")]
    public Task<ExternalCheckoutResponse> Checkout([FromBody] ExternalCheckoutRequest req, CancellationToken ct)
        => _checkout.CheckoutAsync(req, ct);

    /// <summary>Consulta estado da venda — para polling no painel do cliente da loja online.</summary>
    [HttpGet("orders/{vendaId:guid}")]
    [ApiScope("read")]
    public Task<ExternalOrderStatusResponse> GetOrder(Guid vendaId, CancellationToken ct)
        => _checkout.GetOrderAsync(vendaId, ct);

    /// <summary>
    /// Cancela uma venda externa (devolução dentro de 14 dias DL 24/2014).
    /// Cascateia: anula fatura Moloni/InvoiceXpress, repõe stock, anula garantia.
    /// Idempotente — chamar 2x devolve sempre o estado actual.
    /// </summary>
    [HttpPost("orders/{vendaId:guid}/cancel")]
    [ApiScope("write")]
    public Task<ExternalOrderStatusResponse> CancelOrder(Guid vendaId, [FromBody] CancelOrderRequest req, CancellationToken ct)
        => _checkout.CancelOrderAsync(vendaId, req.Motivo, ct);

    /// <summary>
    /// Lista catálogo de Parts ativas para a loja online consultar. Apenas dados públicos —
    /// custo, fornecedor, local armazenamento e notas internas NÃO são expostos.
    /// </summary>
    [HttpGet("parts")]
    [ApiScope("read")]
    public Task<PagedResult<ExternalPartDto>> ListParts(
        [FromQuery] string? search,
        [FromQuery] PartCategoria? categoria,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? lojaOnline = null,
        CancellationToken ct = default)
        => _checkout.ListPartsAsync(search, categoria, page, pageSize, lojaOnline, ct);

    /// <summary>
    /// Histórico do cliente por NIF (vendas + reparações + garantias activas).
    /// Para loja online mostrar "Os meus pedidos" sem replicar BD. 404 se NIF não corresponde.
    /// </summary>
    [HttpGet("clientes/{nif}/historico")]
    [ApiScope("read")]
    public async Task<ActionResult<ExternalClienteHistoricoResponse>> GetHistoricoByNif(string nif, CancellationToken ct)
    {
        var historico = await _checkout.GetHistoricoByNifAsync(nif, ct);
        return historico is null ? NotFound() : Ok(historico);
    }

    /// <summary>
    /// Detalhe de uma garantia por slug, para a loja online mostrar info ao cliente.
    /// 404 se slug não existe no tenant da API key.
    /// </summary>
    [HttpGet("garantias/{slug}")]
    [ApiScope("read")]
    public async Task<ActionResult<ExternalGarantiaDetalhe>> GetGarantia(string slug, CancellationToken ct)
    {
        var g = await _checkout.GetGarantiaBySlugAsync(slug, ct);
        return g is null ? NotFound() : Ok(g);
    }
}
