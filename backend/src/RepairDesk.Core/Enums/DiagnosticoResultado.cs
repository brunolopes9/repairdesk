namespace RepairDesk.Core.Enums;

/// <summary>
/// Resultado de um item do checklist de diagnóstico.
/// </summary>
public enum DiagnosticoResultado
{
    /// <summary>Não testado / não aplicável (não conta para o score).</summary>
    NaoTestado = 0,
    /// <summary>Funciona correctamente.</summary>
    Ok = 1,
    /// <summary>Tem problema (conta como negativo para o score).</summary>
    Avaria = 2,
    /// <summary>Funcional mas marginal (conta como meio negativo).</summary>
    Marginal = 3,
}
