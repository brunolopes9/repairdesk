using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Conteudo;

// Sprint 598 (Doc 95): contratos do módulo Conteúdo.

public sealed record ConteudoFicheiroDto(
    Guid Id,
    Guid? PublicacaoId,
    int Ordem,
    ConteudoFicheiroTipo Tipo,
    ConteudoFicheiroEstado Estado,
    string NomeOriginal,
    string ContentType,
    long Bytes,
    int? Largura,
    int? Altura,
    decimal? DuracaoSeg,
    string? Notas,
    DateTime? ProcessadoEm,
    DateTime CreatedAt,
    /// <summary>URL temporário para ver/descarregar (pré-assinado no R2).</summary>
    string? Url);

public sealed record ConteudoDestinoDto(
    Guid Id,
    RedeSocial Rede,
    string? LegendaPropria,
    ConteudoDestinoEstado Estado,
    string? ExternalId,
    string? Permalink,
    int Tentativas,
    string? UltimoErro,
    DateTime? PublicadoEm,
    TikTokOpcoes? TikTok = null);

public sealed record ConteudoPublicacaoDto(
    Guid Id,
    int Numero,
    ConteudoLinha Linha,
    ConteudoFormato Formato,
    string Titulo,
    string? Legenda,
    DateTime? AgendadoPara,
    ConteudoEstado Estado,
    DateTime? AprovadoEm,
    string? MotivoRejeicao,
    bool AutoAprovada,
    ConteudoOrigem Origem,
    Guid? ArtigoId,
    string? Notas,
    DateTime? PublicadaEm,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ConteudoFicheiroDto> Ficheiros,
    IReadOnlyList<ConteudoDestinoDto> Destinos);

public sealed record ConteudoDestinoInput(RedeSocial Rede, string? LegendaPropria);

/// <summary>Criar ou editar. <see cref="Submeter"/> = enviar logo para aprovação.</summary>
public sealed record GuardarPublicacaoRequest(
    ConteudoLinha Linha,
    ConteudoFormato Formato,
    string Titulo,
    string? Legenda,
    DateTime? AgendadoPara,
    string? Notas,
    Guid? ArtigoId,
    IReadOnlyList<Guid>? FicheiroIds,
    IReadOnlyList<ConteudoDestinoInput>? Destinos,
    bool Submeter = false,
    bool AutoAprovada = false);

public sealed record RejeitarPublicacaoRequest(string? Motivo);

/// <summary>Sprint 610: hora de publicação escolhida pelo dono — <see cref="Agora"/> = sai no próximo minuto.</summary>
public sealed record HoraPublicacaoRequest(DateTime? AgendadoPara, bool Agora = false);

/// <summary>Registo do que aconteceu numa rede (publicação manual nesta fase, automática na Fase B).</summary>
public sealed record AtualizarDestinoRequest(
    ConteudoDestinoEstado Estado,
    string? ExternalId,
    string? Permalink,
    string? Erro);

public sealed record PedirUploadRequest(string NomeOriginal, string ContentType, long Bytes, string? Notas);

public sealed record UploadTicketDto(Guid FicheiroId, string UploadUrl, string Metodo, string ContentType, DateTime ExpiraEm);

public sealed record ConfirmarUploadRequest(int? Largura, int? Altura, decimal? DuracaoSeg);

public sealed record ConteudoResumoDto(int PorAprovar, int BibliotecaPorProcessar);

/// <summary>
/// Sprint 603: o que o dono escolhe antes de um vídeo sair no TikTok. As regras do TikTok para
/// apps que publicam por conta do utilizador obrigam a que seja ele a escolher — privacidade sem
/// valor por omissão, interações desligadas até as ligar, divulgação comercial explícita.
/// </summary>
public sealed record TikTokOpcoes(
    string Privacidade,
    bool PermitirComentarios,
    bool PermitirDueto,
    bool PermitirStitch,
    bool Divulgacao,
    bool SuaMarca,
    bool ConteudoPatrocinado);

/// <summary>Sprint 603: <see cref="Titulo"/> é o texto do post no TikTok (null = a legenda da publicação).</summary>
public sealed record GuardarOpcoesTikTokRequest(
    string? Titulo,
    string? Privacidade,
    bool PermitirComentarios,
    bool PermitirDueto,
    bool PermitirStitch,
    bool Divulgacao,
    bool SuaMarca,
    bool ConteudoPatrocinado);
