namespace RepairDesk.Core.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }
    bool HasTenant { get; }
}
