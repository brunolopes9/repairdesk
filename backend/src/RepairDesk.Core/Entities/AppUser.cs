using Microsoft.AspNetCore.Identity;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class AppUser : IdentityUser<Guid>, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
}
