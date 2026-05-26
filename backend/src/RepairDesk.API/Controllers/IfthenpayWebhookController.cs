using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Payments;
using RepairDesk.Services.Payments.Ifthenpay;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 303 Fase B: callback IFTHENPAY. Chamado quando pagamento MBWay é confirmado
/// na app do cliente OU referência MB é paga em qualquer ATM/homebanking.
///
/// IFTHENPAY envia GET com query params + AntiPhishingKey (shared secret no URL).
/// Não é HMAC clássico — validamos comparação constant-time da chave.
/// </summary>
[ApiController]
[Route("api/payments/webhook/ifthenpay")]
[AllowAnonymous]
public sealed class IfthenpayWebhookController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly IfthenpayOptions _options;
    private readonly ILogger<IfthenpayWebhookController> _logger;

    public IfthenpayWebhookController(IPaymentService payments, IfthenpayOptions options, ILogger<IfthenpayWebhookController> logger)
    {
        _payments = payments;
        _options = options;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Callback(
        [FromQuery(Name = "antiphishing_key")] string? antiPhishingKey,
        [FromQuery(Name = "orderId")] string? orderId,
        [FromQuery(Name = "amount")] string? amount,
        [FromQuery(Name = "requestId")] string? requestId,
        [FromQuery(Name = "status")] string? status,
        [FromQuery(Name = "payment_datetime")] string? paymentDatetime,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AntiPhishingKey))
        {
            _logger.LogWarning("IfthenpayWebhookRejected reason=anti-phishing key não configurada");
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(antiPhishingKey)
            || !CryptographicEquals(antiPhishingKey, _options.AntiPhishingKey))
        {
            _logger.LogWarning("IfthenpayWebhookRejected reason=anti-phishing key mismatch orderId={OrderId}", orderId);
            return Unauthorized();
        }

        // ProviderRef = requestId (MBWay) OU entidade-referencia (MB). Tentamos pelo requestId primeiro.
        var providerRef = !string.IsNullOrWhiteSpace(requestId) ? requestId : orderId;
        if (string.IsNullOrWhiteSpace(providerRef))
        {
            _logger.LogWarning("IfthenpayWebhookRejected reason=missing identifiers");
            return BadRequest();
        }

        var paid = !string.IsNullOrWhiteSpace(status) && status.Equals("PAID", StringComparison.OrdinalIgnoreCase);
        var snapshot = new PaymentStatusSnapshot(
            Status: paid ? PaymentStatus.Pago : PaymentStatus.NaoPago,
            ConfirmedAt: paid ? DateTime.UtcNow : null,
            FailureReason: paid ? null : $"status={status}");

        try
        {
            await _payments.ApplyStatusUpdateAsync(providerRef, snapshot, ct);
            _logger.LogInformation("IfthenpayWebhookApplied providerRef={ProviderRef} status={Status}", providerRef, status);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "IfthenpayWebhookUnknownPayment providerRef={ProviderRef}", providerRef);
            // 200 mesmo se desconhecido — IFTHENPAY não retry indefinidamente em 4xx
            // mas se devolvermos 4xx pode entrar em retry loop. 200 + log é mais seguro.
            return Ok();
        }
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
