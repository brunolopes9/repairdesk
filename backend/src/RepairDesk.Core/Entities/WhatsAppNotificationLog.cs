using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class WhatsAppNotificationLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string EntityType { get; set; } = "Venda";
    public Guid EntityId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public VendaEstado? Estado { get; set; }
    public string? Phone { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
