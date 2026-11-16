using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 682: «Avisa-me quando houver» num produto esgotado da loja. Um email por produto; quando o produto volta
/// a ter stock o Mender envia UM email e apaga a linha (o email só serve para este aviso — RGPD, minimização).
/// </summary>
public class AlertaStockLoja : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Slug do produto na loja (estável depois de publicado).</summary>
    public required string Slug { get; set; }
    /// <summary>Em minúsculas.</summary>
    public required string Email { get; set; }
}
