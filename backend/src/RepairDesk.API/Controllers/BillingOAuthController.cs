using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Billing;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/billing/moloni/oauth")]
public class BillingOAuthController : ControllerBase
{
    private readonly ITenantBillingSettingsService _billing;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BillingOAuthController> _logger;

    public BillingOAuthController(
        ITenantBillingSettingsService billing,
        IConfiguration configuration,
        ILogger<BillingOAuthController> logger)
    {
        _billing = billing;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("start")]
    [Authorize]
    public Task<MoloniOAuthStartDto> Start([FromBody] StartOAuthRequest? body, CancellationToken ct)
    {
        var redirectUri = !string.IsNullOrWhiteSpace(body?.RedirectUri)
            ? body.RedirectUri.Trim()
            : BuildRedirectUri();
        return _billing.StartMoloniOAuthAsync(redirectUri, ct);
    }

    // Web flow (automatico): Moloni redirecciona aqui directamente. Requer URL publico reachable.
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return RedirectToSettings("error", errorDescription ?? error);

        try
        {
            await _billing.CompleteMoloniOAuthAsync(code ?? string.Empty, state ?? string.Empty, ct);
            return RedirectToSettings("connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moloni OAuth callback failed");
            return RedirectToSettings("error", ex.Message);
        }
    }

    // Manual flow (cola URL): para casos em que o callback URL configurado na Moloni nao chega ao RepairDesk
    // (ex: dev local, callback aponta para outro dominio). User cola o URL completo redireccionado pela Moloni.
    [HttpPost("complete")]
    [Authorize]
    public async Task<TenantBillingSettingsDto> Complete([FromBody] CompleteOAuthRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Code) || string.IsNullOrWhiteSpace(body.State))
            throw new RepairDesk.Core.Exceptions.ValidationException(
                "moloni_oauth_missing",
                "Cola o URL completo redireccionado pela Moloni (com code e state).");

        return await _billing.CompleteMoloniOAuthAsync(body.Code.Trim(), body.State.Trim(), ct);
    }

    private string BuildRedirectUri()
    {
        var configured = _configuration["Billing:Moloni:OAuthRedirectUri"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
        return $"{Request.Scheme}://{Request.Host}/api/billing/moloni/oauth/callback";
    }

    private LocalRedirectResult RedirectToSettings(string status, string? message = null)
    {
        var target = $"/definicoes?moloni={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrWhiteSpace(message))
            target += $"&msg={Uri.EscapeDataString(message)}";
        return LocalRedirect(target);
    }

    public sealed record StartOAuthRequest(string? RedirectUri);
    public sealed record CompleteOAuthRequest(string Code, string State);
}
