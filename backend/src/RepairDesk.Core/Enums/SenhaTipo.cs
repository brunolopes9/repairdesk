namespace RepairDesk.Core.Enums;

/// <summary>Sprint 595: como o equipamento se desbloqueia (para o técnico o poder testar).</summary>
public enum SenhaTipo
{
    Nenhuma = 0,
    /// <summary>PIN numérico (4 a 8 dígitos).</summary>
    Pin = 1,
    /// <summary>Palavra-passe com letras e números.</summary>
    Alfanumerica = 2,
    /// <summary>Padrão de 9 pontos (3×3) — guardado como a sequência dos pontos, ex.: "1-5-9-6".</summary>
    Padrao = 3,
}
