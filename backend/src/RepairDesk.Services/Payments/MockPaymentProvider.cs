using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Payments;

/// <summary>
/// Sprint 303: provider de pagamento para dev/tests. Auto-aprova qualquer cobrança
/// imediatamente. NÃO usar em produção — registar via DI só quando
/// <c>ASPNETCORE_ENVIRONMENT</c> = Development/Testing.
/// </summary>
public sealed class MockPaymentProvider : IPaymentProvider
{
    public PaymentProvider Provider => PaymentProvider.Mock;

    public IReadOnlySet<PaymentMethod> SupportedMethods { get; } = new HashSet<PaymentMethod>
    {
        PaymentMethod.MBWay,
        PaymentMethod.Multibanco,
        PaymentMethod.Cartao,
        PaymentMethod.TransferenciaBancaria,
    };

    public Task<PaymentInitiation> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct = default)
    {
        var providerRef = $"mock-{Guid.NewGuid():N}";
        var metadata = JsonSerializer.Serialize(new
        {
            mock = true,
            method = request.Method.ToString(),
            amountCents = request.AmountCents,
        });

        return Task.FromResult(new PaymentInitiation(
            Status: PaymentStatus.Pago,
            ProviderRef: providerRef,
            ExternalId: providerRef,
            MetadataJson: metadata,
            ExpiresAt: null,
            CustomerInstructions: "[mock] Pagamento aprovado automaticamente."));
    }

    public Task<PaymentStatusSnapshot> CheckStatusAsync(string providerRef, CancellationToken ct = default) =>
        Task.FromResult(new PaymentStatusSnapshot(
            Status: PaymentStatus.Pago,
            ConfirmedAt: DateTime.UtcNow,
            FailureReason: null));
}
