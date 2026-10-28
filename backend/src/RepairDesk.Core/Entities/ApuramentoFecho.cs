using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 576: trimestre declarado (e pago) — guarda os valores que foram à declaração. Se depois alguma
/// compra/venda/despesa mexer nesse trimestre, o apuramento mostra a diferença em vez de mudar em silêncio.
/// </summary>
public class ApuramentoFecho : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int Ano { get; set; }
    public int Trimestre { get; set; }
    public decimal IvaLiquidado { get; set; }
    public decimal IvaDedutivel { get; set; }
    /// <summary>Positivo = entregue ao Estado; negativo = a recuperar.</summary>
    public decimal Saldo { get; set; }
    public bool Pago { get; set; }
    public DateTime FechadoEm { get; set; } = DateTime.UtcNow;
}
