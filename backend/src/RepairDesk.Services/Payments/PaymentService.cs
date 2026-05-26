using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Payments;

public interface IPaymentService
{
    Task<Payment> InitiateAsync(PaymentInitiationRequest request, PaymentProvider provider, CancellationToken ct = default);
    Task<Payment?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default);

    /// <summary>
    /// Aplica actualização de estado (chamado pelo webhook ou por polling).
    /// Idempotent: chamadas repetidas com o mesmo estado não duplicam efeitos.
    /// </summary>
    Task<Payment> ApplyStatusUpdateAsync(string providerRef, PaymentStatusSnapshot snapshot, CancellationToken ct = default);
}

/// <summary>
/// Sprint 303: orquestra <see cref="IPaymentProvider"/> + <see cref="IPaymentRepository"/>.
/// Selecciona o provider correcto pelo enum e persiste o resultado da iniciação.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repo;
    private readonly IReadOnlyDictionary<PaymentProvider, IPaymentProvider> _providers;

    public PaymentService(IPaymentRepository repo, IEnumerable<IPaymentProvider> providers)
    {
        _repo = repo;
        _providers = providers.ToDictionary(p => p.Provider);
    }

    public async Task<Payment> InitiateAsync(PaymentInitiationRequest request, PaymentProvider provider, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(provider, out var impl))
            throw new InvalidOperationException($"PaymentProvider '{provider}' não está registado.");

        if (!impl.SupportedMethods.Contains(request.Method))
            throw new InvalidOperationException(
                $"PaymentProvider '{provider}' não suporta o método '{request.Method}'.");

        if (request.AmountCents <= 0)
            throw new ArgumentException("AmountCents tem de ser positivo.", nameof(request));

        var initiation = await impl.InitiateAsync(request, ct);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            VendaId = request.VendaId,
            Method = request.Method,
            Provider = provider,
            AmountCents = request.AmountCents,
            Status = initiation.Status,
            ProviderRef = initiation.ProviderRef,
            ExternalId = initiation.ExternalId,
            MetadataJson = initiation.MetadataJson,
            ExpiresAt = initiation.ExpiresAt,
            ConfirmedAt = initiation.Status == PaymentStatus.Pago ? DateTime.UtcNow : null,
        };

        await _repo.AddAsync(payment, ct);
        return payment;
    }

    public Task<Payment?> GetAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default) =>
        _repo.GetByVendaAsync(vendaId, ct);

    public async Task<Payment> ApplyStatusUpdateAsync(string providerRef, PaymentStatusSnapshot snapshot, CancellationToken ct = default)
    {
        var payment = await _repo.GetByProviderRefAsync(providerRef, ct)
            ?? throw new InvalidOperationException($"Payment com providerRef '{providerRef}' não existe.");

        // Idempotency: terminal states não regridem.
        if (payment.Status is PaymentStatus.Pago or PaymentStatus.Anulado)
            return payment;

        payment.Status = snapshot.Status;
        payment.ConfirmedAt = snapshot.ConfirmedAt ?? payment.ConfirmedAt;
        payment.FailureReason = snapshot.FailureReason ?? payment.FailureReason;
        if (snapshot.Status == PaymentStatus.Pago && payment.ConfirmedAt is null)
            payment.ConfirmedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(payment, ct);
        return payment;
    }
}
