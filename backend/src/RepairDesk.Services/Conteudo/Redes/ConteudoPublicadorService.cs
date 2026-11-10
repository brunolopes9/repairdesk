using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Push;

namespace RepairDesk.Services.Conteudo.Redes;

public interface IConteudoPublicadorService
{
    /// <summary>Publica o que está aprovado e na hora. Devolve quantas publicações processou.</summary>
    Task<int> PublicarPendentesAsync(CancellationToken ct = default);
    /// <summary>Renova tokens do Threads que expiram nos próximos 15 dias.</summary>
    Task RenovarTokensAsync(CancellationToken ct = default);
}

/// <summary>
/// Sprint 599 (Doc 95 Fase B): o publicador automático. Corre fora de pedidos HTTP (todos os
/// tenants) e só toca em tenants com «Publicação automática» ligada.
///
/// Garantias:
/// - reserva a publicação (Aprovada → APublicar) antes de falar com as redes;
/// - só publica destinos <c>Pendente</c> das redes com ligação válida — <c>AgendadoNaRede</c>
///   (agendado à mão no Business Suite) e o TikTok ficam de fora;
/// - grava o ExternalId logo a seguir a cada sucesso, por isso uma falha a meio nunca republica
///   o que já saiu;
/// - falhas temporárias repetem com 2/10/30 min; à 3.ª (ou erro definitivo) o destino fica
///   «Falhou» e o dono recebe push.
/// </summary>
public sealed class ConteudoPublicadorService : IConteudoPublicadorService
{
    public static readonly RedeSocial[] RedesAutomaticas = { RedeSocial.Facebook, RedeSocial.Instagram, RedeSocial.Threads, RedeSocial.TikTok, RedeSocial.YouTube };
    private static readonly TimeSpan[] Backoff = { TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30) };
    private static readonly TimeSpan ValidadeMedia = TimeSpan.FromHours(24);

    private readonly IConteudoRepository _repo;
    private readonly IReadOnlyDictionary<RedeSocial, IPublicadorRede> _publicadores;
    private readonly IDirectAccessStorage? _direct;
    private readonly ISecretProtector _secrets;
    private readonly IStaffPushQueue _push;
    private readonly MetaGraphClient _g;
    private readonly IPhotoStorage _storage;
    private readonly TikTokTokens _tiktok;
    private readonly YouTubeTokens _youtube;
    private readonly ITenantRepository _tenants;
    private readonly ILogger<ConteudoPublicadorService> _logger;

    public ConteudoPublicadorService(
        IConteudoRepository repo,
        IEnumerable<IPublicadorRede> publicadores,
        IEnumerable<IDirectAccessStorage> direct,
        ISecretProtector secrets,
        IStaffPushQueue push,
        MetaGraphClient g,
        IPhotoStorage storage,
        TikTokTokens tiktok,
        YouTubeTokens youtube,
        ITenantRepository tenants,
        ILogger<ConteudoPublicadorService> logger)
    {
        _youtube = youtube;
        _repo = repo;
        _publicadores = publicadores.ToDictionary(p => p.Rede);
        _direct = direct.FirstOrDefault();
        _secrets = secrets;
        _push = push;
        _g = g;
        _storage = storage;
        _tiktok = tiktok;
        _tenants = tenants;
        _logger = logger;
    }

    public async Task<int> PublicarPendentesAsync(CancellationToken ct = default)
    {
        if (_direct is null)
        {
            // Sem URLs públicos (storage local) as redes não conseguem ir buscar os ficheiros.
            return 0;
        }

        var agora = DateTime.UtcNow;
        var ids = await _repo.ListIdsParaPublicarAsync(agora, RedesAutomaticas, ct);
        var feitas = 0;
        foreach (var id in ids)
        {
            if (!await _repo.ReservarParaPublicarAsync(id, ct)) continue;
            var p = await _repo.FindPublicacaoAsync(id, ct);
            if (p is null) continue;
            try
            {
                await PublicarAsync(p, ct);
            }
            finally
            {
                // Se ainda há destinos por fazer (ex.: TikTok manual ou retry), volta a Aprovada.
                if (p.Estado == ConteudoEstado.APublicar) p.Estado = ConteudoEstado.Aprovada;
                await _repo.SaveAsync(CancellationToken.None);
            }
            feitas++;
        }
        return feitas;
    }

    private async Task PublicarAsync(ConteudoPublicacao p, CancellationToken ct)
    {
        var ligacoes = (await _repo.ListLigacoesAsync(p.TenantId, ct)).Where(l => l.Valida).ToDictionary(l => l.Rede);
        var media = p.Ficheiros.OrderBy(f => f.Ordem)
            .Select(f => new MediaPublica(_direct!.CreateDownloadUrl(f.StorageKey, ValidadeMedia), f.Tipo))
            .ToList();
        var agora = DateTime.UtcNow;

        foreach (var d in p.Destinos.Where(d => d.Estado == ConteudoDestinoEstado.Pendente
                                                && RedesAutomaticas.Contains(d.Rede)
                                                && (d.ProximaTentativaEm == null || d.ProximaTentativaEm <= agora)))
        {
            if (!ligacoes.TryGetValue(d.Rede, out var lig) || !_publicadores.TryGetValue(d.Rede, out var publicador))
                continue; // rede não ligada: fica pendente (publicação manual)
            if (d.Rede is RedeSocial.TikTok or RedeSocial.YouTube && p.Formato != ConteudoFormato.Reel)
                continue; // TikTok/YouTube automáticos só para vídeo; fotos ficam à mão
            var opcoesTikTok = d.Rede == RedeSocial.TikTok ? ConteudoService.LerOpcoesTikTok(d.OpcoesJson) : null;
            if (d.Rede == RedeSocial.TikTok && opcoesTikTok is null)
                continue; // Sprint 603: sem as escolhas do dono não se publica no TikTok
            if (!string.IsNullOrEmpty(d.ExternalId))
            {
                // Já saiu numa volta anterior (falha ao gravar depois) — só acerta o estado.
                d.Estado = ConteudoDestinoEstado.Publicado;
                continue;
            }

            try
            {
                var token = d.Rede switch
                {
                    RedeSocial.TikTok => await _tiktok.TokenValidoAsync(lig, ct),
                    RedeSocial.YouTube => await _youtube.TokenValidoAsync(lig, ct),
                    _ => _secrets.Unprotect(lig.TokenCipherText),
                };
                var video = p.Ficheiros.OrderBy(f => f.Ordem).FirstOrDefault(f => f.Tipo == ConteudoFicheiroTipo.Video);
                var r = await publicador.PublicarAsync(new PedidoPublicacao(
                    p.Formato, d.LegendaPropria ?? p.Legenda, media, lig.ContaId, token, opcoesTikTok, video?.DuracaoSeg,
                    video is null ? null : c => _storage.DownloadAsync(video.StorageKey, c)), ct);
                d.ExternalId = r.ExternalId;
                d.Permalink = r.Permalink;
                d.Estado = ConteudoDestinoEstado.Publicado;
                d.PublicadoEm = DateTime.UtcNow;
                d.UltimoErro = null;
                d.ProximaTentativaEm = null;
                await _repo.SaveAsync(CancellationToken.None); // grava já: idempotência
                _logger.LogInformation("Conteúdo #{Numero} publicado no {Rede} ({ExternalId}).", p.Numero, d.Rede, r.ExternalId);
            }
            catch (RedeSocialException ex)
            {
                await RegistarFalha(p, d, lig, ex, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erro inesperado a publicar #{Numero} no {Rede}.", p.Numero, d.Rede);
                await RegistarFalha(p, d, lig, new RedeSocialException("Erro interno a publicar.", temporario: true), ct);
            }
        }

        ConteudoService.RecalcularEstado(p);
    }

    private async Task RegistarFalha(ConteudoPublicacao p, ConteudoDestino d, RedeSocialLigacao lig, RedeSocialException ex, CancellationToken ct)
    {
        d.Tentativas++;
        d.UltimoErro = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
        _logger.LogWarning("Conteúdo #{Numero} falhou no {Rede} (tentativa {N}): {Erro}", p.Numero, d.Rede, d.Tentativas, ex.Message);

        if (ex.TokenInvalido)
        {
            lig.Valida = false;
            lig.UltimoErro = "A rede recusou o acesso — liga a conta outra vez.";
            d.ProximaTentativaEm = DateTime.UtcNow.Add(Backoff[^1]);
            await Avisar(p.TenantId, $"⚠️ {d.Rede}: liga a conta outra vez", "A ligação expirou ou foi revogada. Definições → Redes sociais.", "conteudo-ligacao", ct);
            return;
        }
        if (ex.Temporario && d.Tentativas < Backoff.Length)
        {
            d.ProximaTentativaEm = DateTime.UtcNow.Add(Backoff[d.Tentativas - 1]);
            return;
        }
        d.Estado = ConteudoDestinoEstado.Falhou;
        d.ProximaTentativaEm = null;
        await Avisar(p.TenantId, $"❌ Não saiu no {d.Rede}", $"#{p.Numero} {p.Titulo}: {d.UltimoErro}", $"conteudo-falha-{p.Id:N}", ct);
    }

    public async Task RenovarTokensAsync(CancellationToken ct = default)
    {
        foreach (var l in await _repo.ListLigacoesAExpirarAsync(DateTime.UtcNow.AddDays(15), ct))
        {
            if (l.Rede != RedeSocial.Threads) continue;
            try
            {
                var r = await _g.GetAsync("https://graph.threads.net/refresh_access_token", new Dictionary<string, string?>
                {
                    ["grant_type"] = "th_refresh_token", ["access_token"] = _secrets.Unprotect(l.TokenCipherText),
                }, ct);
                l.TokenCipherText = _secrets.Protect(MetaGraphClient.Str(r, "access_token"));
                l.TokenExpiraEm = DateTime.UtcNow.AddSeconds(long.TryParse(MetaGraphClient.StrOuNull(r, "expires_in"), out var s) ? s : 60 * 86400);
                l.UltimoErro = null;
            }
            catch (RedeSocialException ex)
            {
                l.UltimoErro = $"Não consegui renovar o acesso: {ex.Message}";
                if (ex.TokenInvalido || l.TokenExpiraEm < DateTime.UtcNow) l.Valida = false;
                await Avisar(l.TenantId, "⚠️ Threads: liga a conta outra vez", "O acesso ao Threads está a expirar. Definições → Redes sociais.", "conteudo-ligacao", ct);
            }
        }
        await _repo.SaveAsync(ct);
    }

    private Task Avisar(Guid tenantId, string titulo, string corpo, string tag, CancellationToken ct)
        => _push.EnqueueAsync(new StaffPushJob(tenantId, titulo, corpo, "/conteudo?tab=agenda", tag), ct).AsTask();
}
