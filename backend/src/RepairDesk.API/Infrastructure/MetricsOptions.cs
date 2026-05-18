namespace RepairDesk.API.Infrastructure;

public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    public bool Enabled { get; set; }
    public string[] AllowedIps { get; set; } = [];
    public string? BasicAuthUsername { get; set; }
    public string? BasicAuthPassword { get; set; }
}
