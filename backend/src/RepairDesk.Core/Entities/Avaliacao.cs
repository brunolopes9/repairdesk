using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Avaliação 1-5 estrelas submetida pelo cliente final via portal público.
/// Uma reparação só pode ter 1 avaliação.
/// </summary>
public class Avaliacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>1 a 5 estrelas.</summary>
    public int Score { get; set; }
    public string? Comentario { get; set; }

    /// <summary>Se cliente autorizou expor review publicamente (widget de testemunhos).</summary>
    public bool PublicarTestemunho { get; set; }

    /// <summary>Cliente foi redirecionado para Google Reviews (4-5 estrelas).</summary>
    public bool PedidoGoogleReview { get; set; }
}
