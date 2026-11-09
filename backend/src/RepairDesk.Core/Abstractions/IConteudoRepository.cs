using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 598 (Doc 95): persistência do módulo Conteúdo.</summary>
public interface IConteudoRepository
{
    Task<IReadOnlyList<ConteudoPublicacao>> ListPublicacoesAsync(
        IReadOnlyCollection<ConteudoEstado>? estados,
        DateTime? deUtc,
        DateTime? ateUtc,
        CancellationToken ct = default);

    /// <summary>Com ficheiros e destinos (tracked).</summary>
    Task<ConteudoPublicacao?> FindPublicacaoAsync(Guid id, CancellationToken ct = default);

    Task<int> CountPorAprovarAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ConteudoFicheiro>> ListBibliotecaAsync(bool soPorProcessar, CancellationToken ct = default);
    Task<ConteudoFicheiro?> FindFicheiroAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConteudoFicheiro>> FindFicheirosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task AddAsync(ConteudoPublicacao publicacao, CancellationToken ct = default);
    Task AddAsync(ConteudoFicheiro ficheiro, CancellationToken ct = default);
    void Remove(ConteudoPublicacao publicacao);
    void Remove(ConteudoFicheiro ficheiro);
    void Remove(ConteudoDestino destino);

    // ---- Sprint 599 (Fase B): contas ligadas e publicador automático ----

    Task<IReadOnlyList<RedeSocialLigacao>> ListLigacoesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(RedeSocialLigacao ligacao, CancellationToken ct = default);
    void Remove(RedeSocialLigacao ligacao);

    /// <summary>Ligações Threads que expiram antes de <paramref name="limite"/> (para renovar).</summary>
    Task<IReadOnlyList<RedeSocialLigacao>> ListLigacoesAExpirarAsync(DateTime limite, CancellationToken ct = default);

    /// <summary>
    /// Publicações aprovadas com hora ≤ agora, de tenants com publicação automática ligada, que têm
    /// destinos pendentes nas redes indicadas prontos a tentar (sem backoff ativo). Todos os tenants.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListIdsParaPublicarAsync(DateTime agora, IReadOnlyCollection<RedeSocial> redes, CancellationToken ct = default);

    /// <summary>Aprovada → APublicar só se ainda estiver Aprovada (impede publicação dupla). True se reservou.</summary>
    Task<bool> ReservarParaPublicarAsync(Guid id, CancellationToken ct = default);

    /// <summary>Grava e atribui o nº visível às publicações novas (retry no índice único).</summary>
    Task SaveAsync(CancellationToken ct = default);
}
