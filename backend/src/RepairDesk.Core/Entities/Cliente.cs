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
}
