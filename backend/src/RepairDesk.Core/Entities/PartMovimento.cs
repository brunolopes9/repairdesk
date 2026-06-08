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
    public Guid? VendaId { get; set; }
    public Venda? Venda { get; set; }
    public string? Notas { get; set; }

    /// <summary>
    /// Sprint 525: entrada comprada a fornecedor intra-UE (autoliquidação). Quando true, o custo
    /// NÃO conta como IVA dedutível normal no Relatório IVA (reverse charge = efeito zero; IVA
    /// estrangeiro não é dedutível em PT). Carimbado na aprovação a partir de Fornecedor.IntraUe.
    /// </summary>
    public bool ReverseCharge { get; set; }
}
