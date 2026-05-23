using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Webhooks;

/// <summary>
/// Worker em loop que apanha <see cref="WebhookDelivery"/> Pending da fila e faz POST
/// para o endpoint subscrito, com header HMAC-SHA256 do payload.
///
/// Política de retry exponencial: 1m, 5m, 30m, 2h, 12h (até 5 tentativas). Após esgotar
/// retries → Status=Failed + Subscription.FailureCount++. Se FailureCount >= 10,
/// a subscription é auto-desactivada (DisabledAt) para não bombardear endpoint partido.
/// </summary>
public class WebhookDeliveryHostedService : BackgroundService
{
    private static readonly TimeSpan[] RetryBackoff =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    };
    private const int MaxAttempts = 5;
    private const int AutoDisableThreshold = 10;
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryHostedService> _logger;

    public WebhookDeliveryHostedService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryHostedService started (poll every {Interval}s)", PollInterval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processor batch failed — will retry next poll.");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var hasDue = await db.WebhookDeliveries
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Status == WebhookDeliveryStatus.Pending
                           && d.NextRetryAt != null && d.NextRetryAt <= now, ct);
        if (!hasDue) return;

        var due = await db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Include(d => d.Subscription)
            .Where(d => d.Status == WebhookDeliveryStatus.Pending
                        && d.NextRetryAt != null && d.NextRetryAt <= now)
            .OrderBy(d => d.NextRetryAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        var http = _httpClientFactory.CreateClient("webhook");
        foreach (var delivery in due)
        {
            if (delivery.Subscription is null || !delivery.Subscription.Active || delivery.Subscription.DisabledAt is not null)
            {
                // Subscription foi desactivada entretanto — descarta sem retry.
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.FailedAt = DateTime.UtcNow;
                delivery.LastError = "Subscription inactive/disabled";
                delivery.NextRetryAt = null;
                continue;
            }
            await TryDeliverAsync(http, delivery, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task TryDeliverAsync(HttpClient http, WebhookDelivery delivery, CancellationToken ct)
    {
        var sub = delivery.Subscription!;
        delivery.Attempts++;

        try
        {
            var content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, sub.Url) { Content = content };
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("RepairDesk-Webhook", "1.0"));
            req.Headers.Add("X-RepairDesk-Event", delivery.EventType);
            req.Headers.Add("X-RepairDesk-Delivery", delivery.Id.ToString());
            // Sprint 161a: timestamp + assinatura inclui timestamp para prevenir replay attacks.
            // Stripe-style: signature = HMAC(secret, "{timestamp}.{body}").
            // Consumer rejeita se |now - timestamp| > 5 min (toleranceWindow).
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            req.Headers.Add("X-RepairDesk-Timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            req.Headers.Add("X-RepairDesk-Signature", SignHmac(sub.Secret, $"{timestamp}.{delivery.PayloadJson}"));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);

            delivery.LastResponseCode = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.NextRetryAt = null;
                delivery.LastError = null;
                sub.LastDeliveryAt = DateTime.UtcNow;
                sub.FailureCount = 0;
                return;
            }

            delivery.LastError = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            delivery.LastError = ex.GetType().Name + ": " + ex.Message;
        }

        // Falha — agendar retry ou marcar como Failed.
        if (delivery.Attempts >= MaxAttempts)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.FailedAt = DateTime.UtcNow;
            delivery.NextRetryAt = null;
            sub.FailureCount++;
            if (sub.FailureCount >= AutoDisableThreshold && sub.DisabledAt is null)
            {
                sub.DisabledAt = DateTime.UtcNow;
                _logger.LogWarning("Webhook subscription {SubId} auto-disabled após {Failures} falhas consecutivas.",
                    sub.Id, sub.FailureCount);
            }
        }
        else
        {
            var backoff = RetryBackoff[Math.Min(delivery.Attempts - 1, RetryBackoff.Length - 1)];
            delivery.NextRetryAt = DateTime.UtcNow.Add(backoff);
        }
    }

    private static string SignHmac(string secret, string body)
    {
        // Strip "whsec_" prefix to obter o byte material original. Mantido para compatibilidade
        // com receptores que validam apenas com o secret bruto.
        var key = secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret["whsec_".Length..] : secret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
