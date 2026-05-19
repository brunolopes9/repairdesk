namespace RepairDesk.Services.Push;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public bool Enabled { get; set; } = true;
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
    public string Subject { get; set; } = "mailto:suporte@repairdesk.pt";
    public int TtlSeconds { get; set; } = 24 * 60 * 60;
    public int DeliveredRetentionDays { get; set; } = 30;
}
