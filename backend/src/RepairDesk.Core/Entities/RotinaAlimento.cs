namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 635 (Doc 98 — Alimentação): um alimento de uma refeição da rotina, com a quantidade e os macros dessa
/// quantidade (valores aproximados, editáveis pelo utilizador).
/// </summary>
public class RotinaAlimento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RotinaItemId { get; set; }
    public int Ordem { get; set; }
    public required string Nome { get; set; }
    /// <summary>Texto livre: «150 g», «1 média», «2 fatias».</summary>
    public string? Quantidade { get; set; }
    public decimal Kcal { get; set; }
    public decimal ProteinaG { get; set; }
    public decimal HidratosG { get; set; }
    public decimal GorduraG { get; set; }
}
