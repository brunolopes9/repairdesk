using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class PartMovimento : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PartId { get; set; }
    public Part? Part { get; set; }
    public int Quantidade { get; set; }
    public int StockAntes { get; set; }
    public int StockDepois { get; set; }
    public PartMovimentoMotivo Motivo { get; set; } = PartMovimentoMotivo.AjusteManual;
    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }
    public string? Notas { get; set; }
}
