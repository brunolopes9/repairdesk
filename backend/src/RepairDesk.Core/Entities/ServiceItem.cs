using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 435 (Doc 90 screenshot "Services" RoApp): catálogo de mão-de-obra/serviços
/// pré-definidos. Hoje o Mender obriga a escrever texto livre cada vez que se cobra
/// mão-de-obra ("Troca ecrã iPhone 13"). Com este catálogo: dropdown rápido com nome
/// + preço + garantia pré-configurados.
///
/// Diferente de <see cref="PartKit"/> (combo de peças + labor com preço fixo): aqui
/// é só o serviço unitário (1 linha de mão-de-obra), reutilizável em vários sítios.
/// </summary>
public class ServiceItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    public string? Descricao { get; set; }
    public int PrecoCents { get; set; }
    /// <summary>Dias de garantia oferecidos ao cliente neste serviço (0 = sem garantia explícita).</summary>
    public int GarantiaDiasCliente { get; set; }
    public bool Activo { get; set; } = true;
}
