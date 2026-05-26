using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 303: abstracção sobre gateways de pagamento. Implementações por provider:
/// <list type="bullet">
///   <item><see cref="PaymentProvider.Mock"/> — auto-approve para dev/tests.</item>
///   <item><see cref="PaymentProvider.Ifthenpay"/> — MBWay + Multibanco (Fase B).</item>
/// </list>
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Identifica o provider que esta instância implementa.</summary>
    PaymentProvider Provider { get; }

    /// <summary>Métodos de pagamento suportados pelo provider.</summary>
    IReadOnlySet<PaymentMethod> SupportedMethods { get; }

    /// <summary>
    /// Inicia uma cobrança. O resultado pode ser:
    /// <list type="bullet">
    ///   <item><c>Confirmed</c> imediato (Manual/Mock) — pagamento concluído.</item>
    ///   <item><c>Pending</c> com instruções para o cliente (referência MB, push MBWay).</item>
    /// </list>
    /// </summary>
    Task<PaymentInitiation> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Verifica estado actual de um pagamento iniciado. Usado para polling
    /// ou re-conciliação após webhook em falta.
    /// </summary>
    Task<PaymentStatusSnapshot> CheckStatusAsync(string providerRef, CancellationToken ct = default);
}

public sealed record PaymentInitiationRequest(
    Guid TenantId,
    Guid VendaId,
    PaymentMethod Method,
    int AmountCents,
    string? CustomerPhone = null,
    string? CustomerEmail = null,
    string? Description = null);

public sealed record PaymentInitiation(
    PaymentStatus Status,
    string? ProviderRef,
    string? ExternalId,
    string? MetadataJson,
    DateTime? ExpiresAt,
    string? CustomerInstructions = null);

public sealed record PaymentStatusSnapshot(
    PaymentStatus Status,
    DateTime? ConfirmedAt,
    string? FailureReason);
