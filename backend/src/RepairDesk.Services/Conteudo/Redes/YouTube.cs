using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Conteudo.Redes;

/// <summary>
/// Sprint 608 (Doc 95 Fase B3): HTTP para o OAuth do Google e a YouTube Data API v3. Os erros da API
/// vêm em <c>error.message</c>/<c>error.errors[].reason</c>; os do OAuth em <c>error</c>/<c>error_description</c>.
/// </summary>
public sealed class YouTubeClient
{
    public const string Token = "https://oauth2.googleapis.com/token";
    public const string Api = "https://www.googleapis.com/youtube/v3";
    public const string Upload = "https://www.googleapis.com/upload/youtube/v3/videos";
    public const string Scopes = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly";

    private readonly HttpClient _http;
    public YouTubeClient(HttpClient http) => _http = http;

    public async Task<JsonElement> OAuthTokenAsync(IDictionary<string, string> form, CancellationToken ct)
    {
        using var res = await Enviar(new HttpRequestMessage(HttpMethod.Post, Token) { Content = new FormUrlEncodedContent(form) }, ct);
        var body = await Ler(res, ct);
        if (body.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } erro)
        {
            var desc = body.TryGetProperty("error_description", out var d) ? d.GetString() : erro;
            var segredos = form.Where(kv => kv.Key is "client_secret" or "refresh_token" or "code").Select(kv => kv.Value).Where(v => v.Length >= 6);
            throw new RedeSocialException($"YouTube: {MetaGraphClient.SemSegredos(desc ?? erro, segredos)}", erro is "invalid_grant" ? 190 : null,
                temporario: (int)res.StatusCode >= 500);
        }
        return body;
    }

    /// <summary>Canal da conta autorizada (com conta de marca, é o canal escolhido no ecrã da Google).</summary>
    public async Task<(string Id, string Nome)> MeuCanalAsync(string token, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}/channels?part=snippet&mine=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Enviar(req, ct);
        var body = Verificar(await Ler(res, ct), (int)res.StatusCode);
        var canal = body.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0
            ? items[0]
            : throw new RedeSocialException("Esta conta Google não tem canal de YouTube — cria o canal e liga outra vez.");
        var nome = canal.TryGetProperty("snippet", out var s) ? MetaGraphClient.StrOuNull(s, "title") : null;
        return (MetaGraphClient.Str(canal, "id"), nome ?? "YouTube");
    }

    /// <summary>Upload «resumable» num só pedido: cria a sessão (metadados) e envia o ficheiro inteiro.</summary>
    public async Task<string> EnviarVideoAsync(string token, object metadados, byte[] video, CancellationToken ct)
    {
        var init = new HttpRequestMessage(HttpMethod.Post, $"{Upload}?uploadType=resumable&part=snippet,status")
        {
            Content = JsonContent.Create(metadados),
        };
        init.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        init.Headers.Add("X-Upload-Content-Length", video.Length.ToString());
        init.Headers.Add("X-Upload-Content-Type", "video/mp4");
        string sessao;
        using (var res = await Enviar(init, ct))
        {
            if (!res.IsSuccessStatusCode) Verificar(await Ler(res, ct), (int)res.StatusCode);
            sessao = res.Headers.Location?.ToString() ?? throw new RedeSocialException("O YouTube não abriu o envio do vídeo.", temporario: true);
        }

        var content = new ByteArrayContent(video);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var put = new HttpRequestMessage(HttpMethod.Put, sessao) { Content = content };
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var up = await Enviar(put, ct);
        var body = Verificar(await Ler(up, ct), (int)up.StatusCode);
        return MetaGraphClient.Str(body, "id");
    }

    private async Task<HttpResponseMessage> Enviar(HttpRequestMessage req, CancellationToken ct)
    {
        try { return await _http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new RedeSocialException($"Sem ligação ao YouTube: {ex.Message}", temporario: true); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new RedeSocialException("O YouTube demorou demasiado a responder.", temporario: true); }
    }

    private static async Task<JsonElement> Ler(HttpResponseMessage res, CancellationToken ct)
    {
        try { return (await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).Clone(); }
        catch (JsonException) { throw new RedeSocialException($"Resposta inesperada do YouTube ({(int)res.StatusCode}).", temporario: (int)res.StatusCode >= 500); }
    }

    public static JsonElement Verificar(JsonElement body, int status)
    {
        if (!body.TryGetProperty("error", out var e) || e.ValueKind != JsonValueKind.Object) return body;
        var msg = MetaGraphClient.StrOuNull(e, "message") ?? $"erro {status}";
        var motivo = e.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0
            ? MetaGraphClient.StrOuNull(errs[0], "reason") : null;
        // quotaExceeded: a quota diária (10 000 unidades; 1600 por vídeo) volta à meia-noite do Pacífico.
        var temporario = status >= 500 || motivo is "quotaExceeded" or "rateLimitExceeded" or "backendError";
        throw new RedeSocialException($"YouTube: {msg}" + (motivo is null ? "" : $" ({motivo})"), status == 401 ? 190 : null, temporario);
    }
}

/// <summary>
/// Tokens do Google: o de acesso dura 1 h e o de renovação não expira (salvo revogação, ou 7 dias
/// enquanto o projeto estiver em modo «Testing»). Guardam-se os dois cifrados na ligação.
/// </summary>
public sealed class YouTubeTokens
{
    private sealed record Par(string A, string R);

    private readonly YouTubeClient _yt;
    private readonly ISecretProtector _secrets;
    private readonly ITenantRepository _tenants;

    public YouTubeTokens(YouTubeClient yt, ISecretProtector secrets, ITenantRepository tenants)
    {
        _yt = yt;
        _secrets = secrets;
        _tenants = tenants;
    }

    public string Cifrar(string access, string refresh) => _secrets.Protect(JsonSerializer.Serialize(new Par(access, refresh)));

    public async Task<string> TokenValidoAsync(RedeSocialLigacao l, CancellationToken ct)
    {
        var par = JsonSerializer.Deserialize<Par>(_secrets.Unprotect(l.TokenCipherText))
                  ?? throw new RedeSocialException("Ligação ao YouTube corrompida — liga outra vez.", 190);
        if (l.TokenExpiraEm is { } exp && exp > DateTime.UtcNow.AddMinutes(5)) return par.A;

        var t = await _tenants.FindByIdAsync(l.TenantId, ct) ?? throw new RedeSocialException("Tenant não encontrado.");
        if (t.YouTubeClientId is null || t.YouTubeClientSecretCipherText is null)
            throw new RedeSocialException("Faltam os dados do cliente OAuth do YouTube.", 190);
        var r = await _yt.OAuthTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = t.YouTubeClientId,
            ["client_secret"] = _secrets.Unprotect(t.YouTubeClientSecretCipherText),
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = par.R,
        }, ct);
        var access = MetaGraphClient.Str(r, "access_token");
        l.TokenCipherText = Cifrar(access, MetaGraphClient.StrOuNull(r, "refresh_token") ?? par.R);
        l.TokenExpiraEm = DateTime.UtcNow.AddSeconds(long.TryParse(MetaGraphClient.StrOuNull(r, "expires_in"), out var s) ? s : 3600);
        return access;
    }
}

/// <summary>
/// Shorts: só reels (vídeo). Antes de enviar confirma que o acesso ainda é do canal ligado — com
/// várias contas de marca na mesma conta Google, nunca se publica no canal errado.
/// Projetos Google ainda não auditados: o YouTube deixa os vídeos em privado até à auditoria.
/// </summary>
public sealed class YouTubePublicador : IPublicadorRede
{
    private const int MaxTitulo = 100;
    private const int MaxDescricao = 5000;
    /// <summary>Categoria «Ciência e tecnologia».</summary>
    private const string Categoria = "28";

    private readonly YouTubeClient _yt;
    public YouTubePublicador(YouTubeClient yt) => _yt = yt;
    public RedeSocial Rede => RedeSocial.YouTube;

    public async Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
    {
        if (p.Formato != ConteudoFormato.Reel || p.AbrirVideo is null)
            throw new RedeSocialException("No YouTube só se publicam vídeos (Shorts).");

        var (canalId, canalNome) = await _yt.MeuCanalAsync(p.Token, ct);
        if (canalId != p.ContaId)
            throw new RedeSocialException($"O acesso ao YouTube é agora do canal «{canalNome}», não do canal ligado — liga outra vez e escolhe o canal certo.", 190);

        byte[] video;
        await using (var s = await p.AbrirVideo(ct))
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms, ct);
            video = ms.ToArray();
        }

        var (titulo, descricao) = Textos(p.Legenda, curto: p.DuracaoSeg is null or <= 180);
        var id = await _yt.EnviarVideoAsync(p.Token, new
        {
            snippet = new { title = titulo, description = descricao, categoryId = Categoria, defaultLanguage = "pt-PT", defaultAudioLanguage = "pt-PT" },
            status = new { privacyStatus = "public", selfDeclaredMadeForKids = false },
        }, video, ct);
        return new Publicado(id, $"https://www.youtube.com/shorts/{id}");
    }

    /// <summary>
    /// Título = 1.ª linha da legenda (até 100 caracteres, com «#Shorts» se couber); descrição = a
    /// legenda toda. O YouTube recusa «&lt;» e «&gt;» nestes campos.
    /// </summary>
    public static (string Titulo, string Descricao) Textos(string? legenda, bool curto)
    {
        static string Limpar(string s) => s.Replace("<", "").Replace(">", "").Trim();
        var texto = Limpar(legenda ?? "");
        var primeira = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        if (primeira.Length == 0) primeira = "LopesTech";
        var titulo = primeira.Length > MaxTitulo ? primeira[..(MaxTitulo - 1)].TrimEnd() + "…" : primeira;
        if (curto && !titulo.Contains("#shorts", StringComparison.OrdinalIgnoreCase) && titulo.Length + 8 <= MaxTitulo)
            titulo += " #Shorts";
        var descricao = texto;
        // O limite do YouTube é em bytes (UTF-8), não em caracteres.
        while (Encoding.UTF8.GetByteCount(descricao) > MaxDescricao) descricao = descricao[..^1];
        return (titulo, descricao);
    }
}
