using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);
    /// <summary>Devolve entregas Pending cujo NextRetryAt expirou — para o processor.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(int max, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
