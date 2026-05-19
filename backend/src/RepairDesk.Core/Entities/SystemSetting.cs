namespace RepairDesk.Core.Entities;

public class SystemSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
