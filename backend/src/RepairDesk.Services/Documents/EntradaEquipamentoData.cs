namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 450 (Doc 91 ponto 4): payload do "Comprovativo de entrada de equipamento".
/// Reusa <see cref="OrcamentoEmissor"/> e <see cref="OrcamentoCliente"/> para evitar
/// duplicação. O campo <see cref="EstadoFisico"/> é opcional — capturado pelo staff
/// no ato de entrada (ex: "ecrã rachado canto inferior direito, sem capa").
/// </summary>
public sealed record EntradaEquipamentoData(
    string Numero,
    DateTime RecebidoEm,
    OrcamentoEmissor Emissor,
    OrcamentoCliente Cliente,
    string Equipamento,
    string? Imei,
    string Avaria,
    string? EstadoFisico,
    /// <summary>Sprint 490: categoria do equipamento (DeviceCategory, S475) já com label pt-PT. Opcional.</summary>
    string? Tipo,
    string? RecebidoPor,
    IReadOnlyList<OrcamentoCampoEquipamento>? CamposEquipamento = null,
    string? TermosLoja = null,
    string? PortalUrl = null);
