using System.Text.Json;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Conteudo.Redes;

public sealed record MediaPublica(string Url, ConteudoFicheiroTipo Tipo);

public sealed record PedidoPublicacao(
    ConteudoFormato Formato,
    string? Legenda,
    IReadOnlyList<MediaPublica> Media,
    string ContaId,
    string Token,
    /// <summary>Sprint 603: escolhas do dono para o TikTok (privacidade, interações, divulgação).</summary>
    TikTokOpcoes? TikTok = null,
    /// <summary>Duração do vídeo (TikTok recusa acima do máximo da conta).</summary>
    decimal? DuracaoSeg = null,
    /// <summary>Abre o 1.º vídeo do storage (TikTok envia o ficheiro, não um URL).</summary>
    Func<CancellationToken, Task<Stream>>? AbrirVideo = null);

public sealed record Publicado(string ExternalId, string? Permalink);

/// <summary>Sprint 599 (Doc 95 Fase B): publica uma publicação numa rede concreta.</summary>
public interface IPublicadorRede
{
    RedeSocial Rede { get; }
    Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct);
}

/// <summary>Espera pelos containers de vídeo (Instagram/Threads) — a rede processa o vídeo antes de publicar.</summary>
internal static class EsperaContainer
{
    public static async Task AteProntoAsync(Func<CancellationToken, Task<(string Estado, string? Erro)>> estado, TimeSpan maximo, CancellationToken ct)
    {
        var fim = DateTime.UtcNow.Add(maximo);
        while (true)
        {
            var (e, erro) = await estado(ct);
            switch (e.ToUpperInvariant())
            {
                case "FINISHED":
                case "PUBLISHED":
                    return;
                case "ERROR":
                case "EXPIRED":
                    throw new RedeSocialException($"A rede não aceitou o ficheiro: {erro ?? e}.");
            }
            if (DateTime.UtcNow > fim)
                throw new RedeSocialException("A rede ainda está a processar o vídeo — tento outra vez daqui a pouco.", temporario: true);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}

/// <summary>
/// Página de Facebook: foto (/photos), carrossel de fotos (fotos não publicadas + /feed com
/// attached_media), reel (/video_reels em 3 fases com file_url) e story (/photo_stories ou /video_stories).
/// </summary>
public sealed class FacebookPublicador : IPublicadorRede
{
    private readonly MetaGraphClient _g;
    public FacebookPublicador(MetaGraphClient g) => _g = g;
    public RedeSocial Rede => RedeSocial.Facebook;

    public async Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
    {
        var page = p.ContaId;
        switch (p.Formato)
        {
            case ConteudoFormato.Post:
            {
                var r = await _g.PostAsync($"{_g.GraphBase}/{page}/photos",
                    new Dictionary<string, string?> { ["url"] = p.Media[0].Url, ["message"] = p.Legenda, ["published"] = "true", ["access_token"] = p.Token }, ct);
                var postId = MetaGraphClient.StrOuNull(r, "post_id") ?? MetaGraphClient.Str(r, "id");
                return new Publicado(postId, await PermalinkAsync(postId, p.Token, ct));
            }
            case ConteudoFormato.Carrossel:
            {
                if (p.Media.Any(m => m.Tipo == ConteudoFicheiroTipo.Video))
                    throw new RedeSocialException("No Facebook o carrossel automático só aceita fotos.");
                var form = new Dictionary<string, string?> { ["message"] = p.Legenda, ["access_token"] = p.Token };
                for (var i = 0; i < p.Media.Count; i++)
                {
                    var foto = await _g.PostAsync($"{_g.GraphBase}/{page}/photos",
                        new Dictionary<string, string?> { ["url"] = p.Media[i].Url, ["published"] = "false", ["access_token"] = p.Token }, ct);
                    form[$"attached_media[{i}]"] = JsonSerializer.Serialize(new { media_fbid = MetaGraphClient.Str(foto, "id") });
                }
                var post = await _g.PostAsync($"{_g.GraphBase}/{page}/feed", form, ct);
                var id = MetaGraphClient.Str(post, "id");
                return new Publicado(id, await PermalinkAsync(id, p.Token, ct));
            }
            case ConteudoFormato.Reel:
            {
                var videoId = await CarregarVideoAsync($"{_g.GraphBase}/{page}/video_reels", p.Media[0].Url, p.Token, ct);
                await _g.PostAsync($"{_g.GraphBase}/{page}/video_reels", new Dictionary<string, string?>
                {
                    ["upload_phase"] = "finish", ["video_id"] = videoId, ["video_state"] = "PUBLISHED",
                    ["description"] = p.Legenda, ["access_token"] = p.Token,
                }, ct);
                return new Publicado(videoId, $"https://www.facebook.com/reel/{videoId}");
            }
            case ConteudoFormato.Story:
            {
                var m = p.Media[0];
                if (m.Tipo == ConteudoFicheiroTipo.Imagem)
                {
                    var foto = await _g.PostAsync($"{_g.GraphBase}/{page}/photos",
                        new Dictionary<string, string?> { ["url"] = m.Url, ["published"] = "false", ["access_token"] = p.Token }, ct);
                    var story = await _g.PostAsync($"{_g.GraphBase}/{page}/photo_stories",
                        new Dictionary<string, string?> { ["photo_id"] = MetaGraphClient.Str(foto, "id"), ["access_token"] = p.Token }, ct);
                    return new Publicado(MetaGraphClient.StrOuNull(story, "post_id") ?? MetaGraphClient.Str(foto, "id"), null);
                }
                var videoId = await CarregarVideoAsync($"{_g.GraphBase}/{page}/video_stories", m.Url, p.Token, ct);
                var fim = await _g.PostAsync($"{_g.GraphBase}/{page}/video_stories",
                    new Dictionary<string, string?> { ["upload_phase"] = "finish", ["video_id"] = videoId, ["access_token"] = p.Token }, ct);
                return new Publicado(MetaGraphClient.StrOuNull(fim, "post_id") ?? videoId, null);
            }
            default:
                throw new RedeSocialException("Este formato não sai no Facebook.");
        }
    }

    /// <summary>Fase start + upload por file_url (o Facebook vai buscar o vídeo ao R2). Devolve o video_id.</summary>
    private async Task<string> CarregarVideoAsync(string endpoint, string videoUrl, string token, CancellationToken ct)
    {
        var start = await _g.PostAsync(endpoint, new Dictionary<string, string?> { ["upload_phase"] = "start", ["access_token"] = token }, ct);
        var videoId = MetaGraphClient.Str(start, "video_id");
        var uploadUrl = MetaGraphClient.StrOuNull(start, "upload_url") ?? $"https://rupload.facebook.com/video-upload/{_g.GraphBase.Split('/').Last()}/{videoId}";
        await _g.PostAsync(uploadUrl, new Dictionary<string, string?>(), ct,
            new Dictionary<string, string> { ["Authorization"] = $"OAuth {token}", ["file_url"] = videoUrl });
        return videoId;
    }

    private async Task<string?> PermalinkAsync(string id, string token, CancellationToken ct)
    {
        try
        {
            var r = await _g.GetAsync($"{_g.GraphBase}/{id}", new Dictionary<string, string?> { ["fields"] = "permalink_url", ["access_token"] = token }, ct);
            return MetaGraphClient.StrOuNull(r, "permalink_url");
        }
        catch (RedeSocialException)
        {
            return null; // já saiu; o link é só conveniência
        }
    }
}

/// <summary>
/// Instagram (conta profissional): cria o container (imagem, REELS, STORIES ou CAROUSEL com filhos),
/// espera que fique FINISHED e publica com media_publish.
/// </summary>
public sealed class InstagramPublicador : IPublicadorRede
{
    private readonly MetaGraphClient _g;
    public InstagramPublicador(MetaGraphClient g) => _g = g;
    public RedeSocial Rede => RedeSocial.Instagram;

    public async Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
    {
        var ig = p.ContaId;
        string container;
        switch (p.Formato)
        {
            case ConteudoFormato.Post:
                container = await CriarAsync(ig, p.Token, Item(p.Media[0], semCarrossel: true), p.Legenda, ct);
                break;
            case ConteudoFormato.Reel:
                container = await CriarAsync(ig, p.Token,
                    new() { ["media_type"] = "REELS", ["video_url"] = p.Media[0].Url, ["share_to_feed"] = "true" }, p.Legenda, ct);
                break;
            case ConteudoFormato.Story:
            {
                var m = p.Media[0];
                container = await CriarAsync(ig, p.Token, m.Tipo == ConteudoFicheiroTipo.Video
                    ? new() { ["media_type"] = "STORIES", ["video_url"] = m.Url }
                    : new() { ["media_type"] = "STORIES", ["image_url"] = m.Url }, null, ct);
                break;
            }
            case ConteudoFormato.Carrossel:
            {
                var filhos = new List<string>();
                foreach (var m in p.Media)
                {
                    var item = Item(m, semCarrossel: false);
                    item["is_carousel_item"] = "true";
                    filhos.Add(await CriarAsync(ig, p.Token, item, null, ct));
                }
                container = await CriarAsync(ig, p.Token,
                    new() { ["media_type"] = "CAROUSEL", ["children"] = string.Join(',', filhos) }, p.Legenda, ct);
                break;
            }
            default:
                throw new RedeSocialException("Este formato não sai no Instagram.");
        }

        var publicado = await _g.PostAsync($"{_g.GraphBase}/{ig}/media_publish",
            new Dictionary<string, string?> { ["creation_id"] = container, ["access_token"] = p.Token }, ct);
        var id = MetaGraphClient.Str(publicado, "id");
        string? link = null;
        try
        {
            var r = await _g.GetAsync($"{_g.GraphBase}/{id}", new Dictionary<string, string?> { ["fields"] = "permalink", ["access_token"] = p.Token }, ct);
            link = MetaGraphClient.StrOuNull(r, "permalink");
        }
        catch (RedeSocialException) { }
        return new Publicado(id, link);
    }

    private static Dictionary<string, string?> Item(MediaPublica m, bool semCarrossel) => m.Tipo == ConteudoFicheiroTipo.Video
        // Vídeo solto no feed do Instagram passou a ser sempre reel.
        ? new() { ["media_type"] = semCarrossel ? "REELS" : "VIDEO", ["video_url"] = m.Url }
        : new() { ["image_url"] = m.Url };

    private async Task<string> CriarAsync(string ig, string token, Dictionary<string, string?> campos, string? legenda, CancellationToken ct)
    {
        campos["access_token"] = token;
        if (legenda is not null) campos["caption"] = legenda;
        var r = await _g.PostAsync($"{_g.GraphBase}/{ig}/media", campos, ct);
        var id = MetaGraphClient.Str(r, "id");
        await EsperaContainer.AteProntoAsync(async c =>
        {
            var s = await _g.GetAsync($"{_g.GraphBase}/{id}", new Dictionary<string, string?> { ["fields"] = "status_code,status", ["access_token"] = token }, c);
            return (MetaGraphClient.StrOuNull(s, "status_code") ?? "IN_PROGRESS", MetaGraphClient.StrOuNull(s, "status"));
        }, TimeSpan.FromMinutes(5), ct);
        return id;
    }
}

/// <summary>Threads: container (TEXT, IMAGE, VIDEO ou CAROUSEL), espera pelo processamento e threads_publish.</summary>
public sealed class ThreadsPublicador : IPublicadorRede
{
    private readonly MetaGraphClient _g;
    public ThreadsPublicador(MetaGraphClient g) => _g = g;
    public RedeSocial Rede => RedeSocial.Threads;

    public async Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
    {
        var user = p.ContaId;
        string container;
        if (p.Media.Count == 0)
        {
            container = await CriarAsync(user, p.Token, new() { ["media_type"] = "TEXT", ["text"] = p.Legenda }, ct);
        }
        else if (p.Media.Count == 1)
        {
            container = await CriarAsync(user, p.Token, Item(p.Media[0], p.Legenda), ct);
        }
        else
        {
            var filhos = new List<string>();
            foreach (var m in p.Media.Take(20))
            {
                var item = Item(m, null);
                item["is_carousel_item"] = "true";
                filhos.Add(await CriarAsync(user, p.Token, item, ct));
            }
            container = await CriarAsync(user, p.Token,
                new() { ["media_type"] = "CAROUSEL", ["children"] = string.Join(',', filhos), ["text"] = p.Legenda }, ct);
        }

        var publicado = await _g.PostAsync($"{_g.ThreadsBase}/{user}/threads_publish",
            new Dictionary<string, string?> { ["creation_id"] = container, ["access_token"] = p.Token }, ct);
        var id = MetaGraphClient.Str(publicado, "id");
        string? link = null;
        try
        {
            var r = await _g.GetAsync($"{_g.ThreadsBase}/{id}", new Dictionary<string, string?> { ["fields"] = "permalink", ["access_token"] = p.Token }, ct);
            link = MetaGraphClient.StrOuNull(r, "permalink");
        }
        catch (RedeSocialException) { }
        return new Publicado(id, link);
    }

    private static Dictionary<string, string?> Item(MediaPublica m, string? texto) => m.Tipo == ConteudoFicheiroTipo.Video
        ? new() { ["media_type"] = "VIDEO", ["video_url"] = m.Url, ["text"] = texto }
        : new() { ["media_type"] = "IMAGE", ["image_url"] = m.Url, ["text"] = texto };

    private async Task<string> CriarAsync(string user, string token, Dictionary<string, string?> campos, CancellationToken ct)
    {
        campos["access_token"] = token;
        var r = await _g.PostAsync($"{_g.ThreadsBase}/{user}/threads", campos, ct);
        var id = MetaGraphClient.Str(r, "id");
        await EsperaContainer.AteProntoAsync(async c =>
        {
            var s = await _g.GetAsync($"{_g.ThreadsBase}/{id}", new Dictionary<string, string?> { ["fields"] = "status,error_message", ["access_token"] = token }, c);
            return (MetaGraphClient.StrOuNull(s, "status") ?? "IN_PROGRESS", MetaGraphClient.StrOuNull(s, "error_message"));
        }, TimeSpan.FromMinutes(5), ct);
        return id;
    }
}
