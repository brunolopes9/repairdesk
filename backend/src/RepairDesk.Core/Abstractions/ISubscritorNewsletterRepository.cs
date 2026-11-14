using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 661: subscritores da newsletter da loja.</summary>
public interface ISubscritorNewsletterRepository
{
    Task<SubscritorNewsletter?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<SubscritorNewsletter?> FindByConfirmacaoHashAsync(string tokenHash, CancellationToken ct = default);
    Task<SubscritorNewsletter?> FindByCancelamentoAsync(string token, CancellationToken ct = default);
    /// <summary>Todos (ativos, por confirmar e cancelados), mais recentes primeiro.</summary>
    Task<IReadOnlyList<SubscritorNewsletter>> ListAsync(CancellationToken ct = default);
    Task AddAsync(SubscritorNewsletter subscritor, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
