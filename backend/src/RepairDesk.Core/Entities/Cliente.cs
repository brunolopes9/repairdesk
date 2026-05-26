using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class Cliente : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Nif { get; set; }
    public string? Notas { get; set; }

    /// <summary>
    /// Sprint 355 (Doc 83 Pillar 10): alerta curto destacado em todo o lado onde o
    /// cliente aparece (ex: "Paga sempre em dinheiro", "Junta — fatura com NIF",
    /// "Cliente difícil"). Diferente de Notas (texto longo de contexto).
    /// </summary>
    public string? NotaImportante { get; set; }
}
