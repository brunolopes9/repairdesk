using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 452 (Doc 91 ponto 1 — Conversas omnicanal v1): registo manual de comunicações
/// com o cliente no contexto de uma reparação. Não integra canais ainda — é o staff que
/// regista que falou ao telefone, enviou WhatsApp, etc. Resolve o problema real do Bruno:
/// hoje fala WhatsApp e perde rasto.
///
/// Não criamos "ChannelIntegration" agora — só "manual" e "link" (WhatsApp link gerado).
/// Quando houver creds reais (email IMAP, WhatsApp Business API) extendemos.
/// </summary>
public class ReparacaoComunicacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>Cliente denormalizado — query por cliente fica trivial.</summary>
    public Guid ClienteId { get; set; }

    public ComunicacaoTipo Tipo { get; set; }
    public ComunicacaoDirecao Direcao { get; set; }

    /// <summary>Texto livre — max 2000 chars (validador). Pode ser resumo da chamada,
    /// transcript do email, etc.</summary>
    public required string Texto { get; set; }

    /// <summary>Utilizador que registou.</summary>
    public Guid CreatedByUserId { get; set; }
}
