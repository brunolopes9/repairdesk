using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 598 (Doc 95): imagem ou vídeo no storage (R2). Sem publicação = vídeo/foto bruta na
/// biblioteca (ex: vídeo de reparação enviado pelo telemóvel), à espera de ser editado.
/// </summary>
public class ConteudoFicheiro : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid? PublicacaoId { get; set; }
    public ConteudoPublicacao? Publicacao { get; set; }

    /// <summary>Ordem dentro da publicação (slides do carrossel).</summary>
    public int Ordem { get; set; }

    public ConteudoFicheiroTipo Tipo { get; set; }
    public ConteudoFicheiroEstado Estado { get; set; } = ConteudoFicheiroEstado.AEnviar;

    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public required string NomeOriginal { get; set; }
    public long Bytes { get; set; }

    /// <summary>Sprint 638: envio por partes em curso no R2 (null quando acaba ou foi um PUT simples).</summary>
    public string? MultipartUploadId { get; set; }

    public int? Largura { get; set; }
    public int? Altura { get; set; }
    public decimal? DuracaoSeg { get; set; }

    /// <summary>Nota de quem enviou (ex: «troca de ecrã iPhone 12, cliente autorizou»).</summary>
    public string? Notas { get; set; }

    /// <summary>Quando o material bruto foi aproveitado para conteúdo (deixa de aparecer como novo).</summary>
    public DateTime? ProcessadoEm { get; set; }
}
