using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 661: subscritor da newsletter da loja (lopestech.pt). Confirmação dupla (RGPD): só conta como subscrito
/// depois de abrir o link do email. Um email por empresa; voltar a subscrever reaproveita a linha.
/// </summary>
public class SubscritorNewsletter : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Em minúsculas; único por empresa.</summary>
    public required string Email { get; set; }
    /// <summary>Onde se inscreveu (ex.: «loja», «produto», «checkout»). Só para estatística.</summary>
    public string? Origem { get; set; }
    /// <summary>SHA-256 do token de confirmação (o token só vai no email). Null depois de confirmar.</summary>
    public string? TokenConfirmacaoHash { get; set; }
    public DateTime? ConfirmacaoEnviadaEm { get; set; }
    public DateTime? ConfirmadoEm { get; set; }
    /// <summary>Token do link «deixar de receber» (vai em todos os emails). Não dá acesso a mais nada.</summary>
    public required string TokenCancelamento { get; set; }
    public DateTime? CanceladoEm { get; set; }

    public bool Ativo => ConfirmadoEm is not null && CanceladoEm is null;
}
