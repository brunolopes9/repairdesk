namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 451 (Doc 91 ponto 4 — par de S450): payload do "Recibo de entrega".
/// Documenta o ato de devolver o equipamento ao cliente após a reparação.
/// Reusa <see cref="OrcamentoEmissor"/> / <see cref="OrcamentoCliente"/>.
/// </summary>
public sealed record EntregaEquipamentoData(
    string Numero,
    DateTime EntregueEm,
    OrcamentoEmissor Emissor,
    OrcamentoCliente Cliente,
    string Equipamento,
    string? Imei,
    string? Diagnostico,
    string ResumoIntervencao,
    IReadOnlyList<OrcamentoLinha> Linhas,
    int TotalPagoCents,
    int DiasGarantia,
    string? GarantiaCobertura,
    string? EntreguePor,
    string? GarantiaUrl,
    string? PortalUrl = null);
