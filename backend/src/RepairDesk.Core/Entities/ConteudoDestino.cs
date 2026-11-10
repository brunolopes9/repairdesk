using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 598 (Doc 95): a publicação numa rede concreta. Estado próprio porque cada rede pode
/// sair ou falhar sozinha; com <see cref="ExternalId"/> preenchido nunca se volta a publicar.
/// </summary>
public class ConteudoDestino : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid PublicacaoId { get; set; }
    public ConteudoPublicacao? Publicacao { get; set; }

    public RedeSocial Rede { get; set; }

    /// <summary>Legenda específica desta rede (null = usa a da publicação).</summary>
    public string? LegendaPropria { get; set; }

    /// <summary>
    /// Sprint 603: opções escolhidas pelo dono para esta rede, em JSON (TikTok: privacidade, comentários,
    /// dueto, stitch e divulgação comercial — o TikTok exige que seja o utilizador a escolhê-las).
    /// </summary>
    public string? OpcoesJson { get; set; }

    public ConteudoDestinoEstado Estado { get; set; } = ConteudoDestinoEstado.Pendente;

    /// <summary>Id do post na rede (idempotência da publicação automática).</summary>
    public string? ExternalId { get; set; }
    public string? Permalink { get; set; }

    public int Tentativas { get; set; }
    public DateTime? ProximaTentativaEm { get; set; }
    public string? UltimoErro { get; set; }
    public DateTime? PublicadoEm { get; set; }
}
