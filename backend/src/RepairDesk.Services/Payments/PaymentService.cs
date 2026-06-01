using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Push;

namespace RepairDesk.Services.Payments;

public interface IPaymentService
{
    Task<Payment> InitiateAsync(PaymentInitiationRequest request, PaymentProvider provider, CancellationToken ct = default);
    Task<Payment?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default);
    /// <summary>Sprint 493: pagamentos de uma reparação (portal MBWay).</summary>
    Task<IReadOnlyList<Payment>> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);

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
    private readonly IReparacaoRepository _reparacoes;
    private readonly IStaffPushQueue _push;

    public PaymentService(IPaymentRepository repo, IEnumerable<IPaymentProvider> providers, IReparacaoRepository reparacoes, IStaffPushQueue push)
    {
        _repo = repo;
        _providers = providers.ToDictionary(p => p.Provider);
        _reparacoes = reparacoes;
        _push = push;
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
            ReparacaoId = request.ReparacaoId,
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

    public Task<IReadOnlyList<Payment>> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default) =>
        _repo.GetByReparacaoAsync(reparacaoId, ct);

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

        // Sprint 493: pagamento de reparação confirmado pelo portal → marca a reparação como Paga.
        // (Vendas têm o seu próprio fluxo de marcação; aqui só tratamos reparações.)
        if (snapshot.Status == PaymentStatus.Pago && payment.ReparacaoId is { } repId)
        {
            var rep = await _reparacoes.FindByIdAsync(repId, ct);
            if (rep is not null && rep.EstadoPagamento != PaymentStatus.Pago)
            {
                rep.EstadoPagamento = PaymentStatus.Pago;
                await _reparacoes.SaveAsync(ct);

                // Sprint 495: fecha o ciclo operacional — a loja é notificada quando o
                // dinheiro entra (mirror do push "iniciado" do portal). Tag distinta para
                // não ser substituída pela notificação de iniciação.
                var metodo = payment.Method == PaymentMethod.MBWay ? "MBWay" : "Multibanco";
                await _push.EnqueueAsync(new StaffPushJob(
                    rep.TenantId,
                    "✅ Pagamento recebido",
                    $"Reparação #{rep.Numero:D5} · {payment.AmountCents / 100m:F2}€ pago por {metodo}",
                    $"/reparacoes/{rep.Id}",
                    $"pay-ok-{rep.Id}"), ct);
            }
        }
        return payment;
    }
}
