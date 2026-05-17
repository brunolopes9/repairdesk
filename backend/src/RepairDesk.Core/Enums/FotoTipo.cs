namespace RepairDesk.Core.Enums;

/// <summary>
/// Momento em que a foto da reparação foi tirada.
/// </summary>
public enum FotoTipo
{
    /// <summary>Estado do equipamento à entrada (prova legal + transparência).</summary>
    Antes = 0,
    /// <summary>Trabalho em curso (placa-mãe, peça extraída, etc.) — opcionalmente para portal.</summary>
    Durante = 1,
    /// <summary>Resultado final (visível no portal cliente após Entrega).</summary>
    Depois = 2,
}
