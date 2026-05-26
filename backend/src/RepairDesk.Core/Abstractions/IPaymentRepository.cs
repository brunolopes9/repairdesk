using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 303: persistência de transacções de pagamento.</summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetByProviderRefAsync(string providerRef, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
