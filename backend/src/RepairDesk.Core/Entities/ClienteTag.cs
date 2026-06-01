using System.ComponentModel.DataAnnotations;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 480: customer segment label. Used for VIPs, lead source, risk flags,
/// campaign groups, and operational routing.
/// </summary>
public class ClienteTag : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    [MaxLength(40)]
    public required string Nome { get; set; }

    [MaxLength(16)]
    public string CorHex { get; set; } = "#3F3F46";

    public List<ClienteTagAssignment> Assignments { get; set; } = new();
}

public class ClienteTagAssignment : BaseEntity
{
    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public Guid ClienteTagId { get; set; }
    public ClienteTag? ClienteTag { get; set; }
}
