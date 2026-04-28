namespace RepairDesk.Core.Entities;

public class Tenant : BaseEntity
{
    public required string Name { get; set; }
    public string? LegalName { get; set; }
    public string? Nif { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public bool IsActive { get; set; } = true;
}
