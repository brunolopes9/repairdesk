using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class TenantPreferences : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int Version { get; set; } = 1;

    public required string PreferencesJson { get; set; } = "{}";
}
