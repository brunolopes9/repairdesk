using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 598 (Doc 95): uma publicação do calendário editorial (post, carrossel, reel, story ou
/// texto) que sai numa ou mais redes. O Mender é a fonte única de verdade: estado, aprovação e
/// histórico vivem aqui e não em ficheiros ou conversas.
/// </summary>
public class ConteudoPublicacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Nº visível, sequencial por tenant (0 = ainda por atribuir, só em memória).</summary>
    public int Numero { get; set; }

    public ConteudoLinha Linha { get; set; }
    public ConteudoFormato Formato { get; set; }

    /// <summary>Nome interno curto (ex: «Reel tampa traseira Xiaomi»).</summary>
    public required string Titulo { get; set; }

    /// <summary>Legenda base; cada destino pode ter a sua (<see cref="ConteudoDestino.LegendaPropria"/>).</summary>
    public string? Legenda { get; set; }

    /// <summary>Hora de publicação (UTC). Null = sem hora marcada.</summary>
    public DateTime? AgendadoPara { get; set; }

    public ConteudoEstado Estado { get; set; } = ConteudoEstado.Rascunho;

    public Guid? AprovadoPorUserId { get; set; }
    public DateTime? AprovadoEm { get; set; }
    public string? MotivoRejeicao { get; set; }

    /// <summary>Nasce aprovada (ex: foto promo diária com autorização permanente do dono).</summary>
    public bool AutoAprovada { get; set; }

    public ConteudoOrigem Origem { get; set; }

    /// <summary>Artigo promovido (promo diária), para não repetir o mesmo artigo em dias seguidos.</summary>
    public Guid? ArtigoId { get; set; }

    public string? Notas { get; set; }

    /// <summary>Quando o último destino ficou publicado/agendado na rede.</summary>
    public DateTime? PublicadaEm { get; set; }

    public List<ConteudoFicheiro> Ficheiros { get; set; } = new();
    public List<ConteudoDestino> Destinos { get; set; } = new();
}
