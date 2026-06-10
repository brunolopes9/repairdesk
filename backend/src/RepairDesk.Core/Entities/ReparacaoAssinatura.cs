using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 551 (Doc 80 pilar assinaturas / Doc 93 #5): assinatura manuscrita do cliente,
/// recolhida no ecrã (canvas) na entrada ou na entrega do equipamento e estampada nos
/// PDFs respetivos. Vive na BD (e não no storage de ficheiros) de propósito: é prova de
/// aceitação ligada à reparação — backup atómico com o registo, hard-delete RGPD por
/// cascade, e o render do PDF não depende do storage externo. ~5-30 KB por assinatura.
/// </summary>
public class ReparacaoAssinatura : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public AssinaturaTipo Tipo { get; set; }

    /// <summary>PNG do traço (fundo transparente), validado por magic bytes no serviço.</summary>
    public byte[] PngBytes { get; set; } = [];

    public DateTime AssinadaEm { get; set; }
}
