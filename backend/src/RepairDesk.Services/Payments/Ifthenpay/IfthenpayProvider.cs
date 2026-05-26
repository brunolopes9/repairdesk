using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Payments.Ifthenpay;

/// <summary>
/// Sprint 303 Fase B: provider IFTHENPAY real. Suporta MBWay push e referências
/// Multibanco. Inicia cobranças no IFTHENPAY API e devolve estado <c>Pendente</c>.
/// A confirmação chega via webhook (<c>IfthenpayWebhookController</c>).
/// </summary>
public sealed class IfthenpayProvider : IPaymentProvider
{
    private readonly HttpClient _http;
    private readonly IfthenpayOptions _options;
    private readonly ILogger<IfthenpayProvider> _logger;

    public IfthenpayProvider(HttpClient http, IfthenpayOptions options, ILogger<IfthenpayProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public PaymentProvider Provider => PaymentProvider.Ifthenpay;

    public IReadOnlySet<PaymentMethod> SupportedMethods { get; } = new HashSet<PaymentMethod>
    {
        PaymentMethod.MBWay,
        PaymentMethod.Multibanco,
    };

    public async Task<PaymentInitiation> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct = default)
    {
        return request.Method switch
        {
            PaymentMethod.MBWay => await InitiateMbWayAsync(request, ct),
            PaymentMethod.Multibanco => await InitiateMultibancoAsync(request, ct),
            _ => throw new InvalidOperationException($"IFTHENPAY não suporta {request.Method}."),
        };
    }

    private async Task<PaymentInitiation> InitiateMbWayAsync(PaymentInitiationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.MBWayKey))
            throw new InvalidOperationException("IFTHENPAY MBWay key não configurada.");
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            throw new ArgumentException("CustomerPhone obrigatório para MBWay.", nameof(request));

        var orderId = $"venda-{request.VendaId:N}".Substring(0, 32);
        var payload = new MbWayRequest(
            MbWayKey: _options.MBWayKey,
            OrderId: orderId,
            Amount: (request.AmountCents / 100m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            MobileNumber: NormalizePhone(request.CustomerPhone),
            Description: request.Description ?? $"Pagamento {orderId}");

        var resp = await _http.PostAsJsonAsync($"{_options.BaseUrl}/spg/payment/mbway", payload, ct);
        var body = await resp.Content.ReadFromJsonAsync<MbWayResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("IFTHENPAY MBWay devolveu corpo vazio.");

        if (body.Status != "000")
        {
            _logger.LogWarning("IfthenpayMbWayRejected Status={Status} Message={Message}", body.Status, body.Message);
            return new PaymentInitiation(
                Status: PaymentStatus.NaoPago,
                ProviderRef: null,
                ExternalId: orderId,
                MetadataJson: JsonSerializer.Serialize(new { error = body.Message, status = body.Status }),
                ExpiresAt: null,
                CustomerInstructions: $"Erro IFTHENPAY: {body.Message}");
        }

        // MBWay push expira tipicamente em 4 min — cliente confirma na app.
        return new PaymentInitiation(
            Status: PaymentStatus.NaoPago,
            ProviderRef: body.RequestId,
            ExternalId: orderId,
            MetadataJson: JsonSerializer.Serialize(new { mbWayRequestId = body.RequestId, phone = payload.MobileNumber }),
            ExpiresAt: DateTime.UtcNow.AddMinutes(4),
            CustomerInstructions: "Confirma o pagamento na app MBWay (4 minutos).");
    }

    private async Task<PaymentInitiation> InitiateMultibancoAsync(PaymentInitiationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.MultibancoKey))
            throw new InvalidOperationException("IFTHENPAY MB key não configurada.");

        var orderId = $"venda-{request.VendaId:N}".Substring(0, 32);
        var url = $"{_options.BaseUrl}/multibanco/reference/sandbox" +
                  $"?mbKey={Uri.EscapeDataString(_options.MultibancoKey)}" +
                  $"&orderId={Uri.EscapeDataString(orderId)}" +
                  $"&amount={(request.AmountCents / 100m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

        var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadFromJsonAsync<MultibancoResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("IFTHENPAY MB devolveu corpo vazio.");

        if (body.Status != "0")
        {
            _logger.LogWarning("IfthenpayMbRejected Status={Status} Message={Message}", body.Status, body.Message);
            return new PaymentInitiation(
                Status: PaymentStatus.NaoPago,
                ProviderRef: null,
                ExternalId: orderId,
                MetadataJson: JsonSerializer.Serialize(new { error = body.Message, status = body.Status }),
                ExpiresAt: null);
        }

        // Refs MB tipicamente expiram em 72h.
        return new PaymentInitiation(
            Status: PaymentStatus.NaoPago,
            ProviderRef: $"{body.Entidade}-{body.Referencia}",
            ExternalId: orderId,
            MetadataJson: JsonSerializer.Serialize(new
            {
                entidade = body.Entidade,
                referencia = body.Referencia,
                amount = request.AmountCents / 100m,
            }),
            ExpiresAt: DateTime.UtcNow.AddHours(72),
            CustomerInstructions: $"Entidade {body.Entidade} • Referência {body.Referencia} • {request.AmountCents / 100m:F2}€");
    }

    public Task<PaymentStatusSnapshot> CheckStatusAsync(string providerRef, CancellationToken ct = default)
    {
        // IFTHENPAY não expõe API de status pull — só webhook push. Devolve "ainda pendente"
        // até webhook chamar /api/payments/webhook/ifthenpay e disparar ApplyStatusUpdateAsync.
        return Task.FromResult(new PaymentStatusSnapshot(
            Status: PaymentStatus.NaoPago,
            ConfirmedAt: null,
            FailureReason: null));
    }

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // IFTHENPAY exige formato 351#9xxxxxxxx
        if (digits.StartsWith("351") && digits.Length == 12) return $"351#{digits[3..]}";
        if (digits.Length == 9 && digits.StartsWith('9')) return $"351#{digits}";
        return $"351#{digits}";
    }

    private sealed record MbWayRequest(
        [property: JsonPropertyName("MbWayKey")] string MbWayKey,
        [property: JsonPropertyName("OrderId")] string OrderId,
        [property: JsonPropertyName("Amount")] string Amount,
        [property: JsonPropertyName("MobileNumber")] string MobileNumber,
        [property: JsonPropertyName("Description")] string Description);

    private sealed record MbWayResponse(
        [property: JsonPropertyName("Status")] string Status,
        [property: JsonPropertyName("Message")] string? Message,
        [property: JsonPropertyName("RequestId")] string? RequestId);

    private sealed record MultibancoResponse(
        [property: JsonPropertyName("Status")] string Status,
        [property: JsonPropertyName("Message")] string? Message,
        [property: JsonPropertyName("Entidade")] string? Entidade,
        [property: JsonPropertyName("Referencia")] string? Referencia);
}
