using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 421 (Doc 90 Tier 1 #3): inventário físico anual/periódico.
///
/// Fluxo:
///   Open()  → snapshot do <see cref="Part.QtdStock"/> de todas as peças activas (cria Items).
///   Count() → operador regista qtdContada em cada item enquanto percorre prateleiras.
///   Close() → para cada item com diferença != 0, cria <see cref="PartMovimento"/>
///             (motivo AjusteManual) que actualiza o stock real. Marca status Concluído.
///
/// Regra de exclusividade: apenas 1 StockTake pode estar em estado Aberto por tenant
/// (evita inventários concorrentes a competir pelo mesmo número físico). Para abrir um
/// novo é preciso fechar ou cancelar o anterior.
/// </summary>
public class StockTake : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid OpenedByUserId { get; set; }

    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public StockTakeStatus Status { get; set; } = StockTakeStatus.Aberto;
    public string? Notas { get; set; }

    public List<StockTakeItem> Items { get; set; } = new();
}

/// <summary>Sprint 421: linha individual de um StockTake — uma por <see cref="Part"/>.</summary>
public class StockTakeItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid StockTakeId { get; set; }
    public StockTake? StockTake { get; set; }

    public Guid PartId { get; set; }
    public Part? Part { get; set; }

    /// <summary>Qtd em sistema no momento de abrir o inventário (snapshot, imutável).</summary>
    public int QtdSistema { get; set; }

    /// <summary>Qtd física contada pelo operador. Null = ainda não contado.</summary>
    public int? QtdContada { get; set; }

    public DateTime? ContadoEm { get; set; }
    public Guid? ContadoByUserId { get; set; }
}
