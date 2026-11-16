using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 687: lista de compras da oficina — o que é preciso encomendar e não está ligado a nenhuma reparação
/// (ex.: «película Redmi 15C ×5»). Por encomendar → encomendado → recebido (sai da lista).
/// </summary>
public class ItemParaEncomendar : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Descricao { get; set; }
    public int Quantidade { get; set; } = 1;
    public string? Fornecedor { get; set; }
    public string? Nota { get; set; }
    /// <summary>0 = por encomendar, 1 = encomendado (a caminho), 2 = recebido.</summary>
    public int Estado { get; set; }
    public DateTime? EncomendadoEm { get; set; }
    public DateTime? RecebidoEm { get; set; }
}
