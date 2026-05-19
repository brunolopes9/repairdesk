using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class VendaItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VendaId { get; set; }
    public Venda? Venda { get; set; }

    public Guid? PartId { get; set; }
    public Part? Part { get; set; }

    public required string Descricao { get; set; }
    public int Quantidade { get; set; }
    public int PrecoUnitarioCents { get; set; }
    public int DescontoCents { get; set; }
    public decimal IvaRate { get; set; }

    public int TotalCents => Math.Max(0, Quantidade * PrecoUnitarioCents - DescontoCents);
}
