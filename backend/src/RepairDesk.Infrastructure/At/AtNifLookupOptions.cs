namespace RepairDesk.Infrastructure.At;

public sealed class AtNifLookupOptions
{
    public const string SectionName = "AtNifLookup";

    public bool Production { get; set; }
    public string TestEndpoint { get; set; } = "https://servicostst.portaldasfinancas.gov.pt:701/sgdtoi/dadosTOI";
    public string ProductionEndpoint { get; set; } = "https://servicos.portaldasfinancas.gov.pt/sgdtoi/dadosTOI";
    public string? EndpointOverride { get; set; }
    public string? CertPath { get; set; }
    public string? KeyPath { get; set; }
    public string? KeyPassword { get; set; }
    public int CacheTtlDays { get; set; } = 30;
    public int MaxDailyCallsPerTenant { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 8;

    public string Endpoint => !string.IsNullOrWhiteSpace(EndpointOverride)
        ? EndpointOverride
        : Production ? ProductionEndpoint : TestEndpoint;
}
