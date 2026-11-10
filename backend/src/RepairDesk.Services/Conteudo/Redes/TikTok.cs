using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Conteudo.Redes;

/// <summary>
/// Sprint 601 (Doc 95 Fase B2): HTTP para open.tiktokapis.com. As respostas da API trazem sempre
/// <c>error.code</c> («ok» quando correu bem) e o OAuth devolve <c>error</c>/<c>error_description</c>.
/// </summary>
public sealed class TikTokClient
{
    public const string Api = "https://open.tiktokapis.com/v2";
    private readonly HttpClient _http;

    public TikTokClient(HttpClient http) => _http = http;

    public async Task<JsonElement> OAuthTokenAsync(IDictionary<string, string> form, CancellationToken ct)
    {
        using var res = await Enviar(new HttpRequestMessage(HttpMethod.Post, $"{Api}/oauth/token/") { Content = new FormUrlEncodedContent(form) }, ct);
        var body = await Ler(res, ct);
        if (body.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } erro)
        {
            var desc = body.TryGetProperty("error_description", out var d) ? d.GetString() : erro;
            throw new RedeSocialException($"TikTok: {desc}", erro is "invalid_grant" ? 190 : null);
        }
        return body;
    }

    public async Task<JsonElement> PostJsonAsync(string path, string token, object payload, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{Api}{path}") { Content = JsonContent.Create(payload) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Enviar(req, ct);
        return Dados(await Ler(res, ct), (int)res.StatusCode);
    }

    public async Task<JsonElement> GetAsync(string pathEQuery, string token, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}{pathEQuery}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Enviar(req, ct);
        return Dados(await Ler(res, ct), (int)res.StatusCode);
    }

    /// <summary>Envia o vídeo para o upload_url devolvido pelo init (um só bloco).</summary>
    public async Task EnviarVideoAsync(string uploadUrl, byte[] video, CancellationToken ct)
    {
        var content = new ByteArrayContent(video);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Headers.ContentRange = new ContentRangeHeaderValue(0, video.Length - 1, video.Length);
        using var res = await Enviar(new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content }, ct);
        if (!res.IsSuccessStatusCode)
            throw new RedeSocialException($"TikTok recusou o envio do vídeo ({(int)res.StatusCode}).", temporario: (int)res.StatusCode >= 500);
    }

    private async Task<HttpResponseMessage> Enviar(HttpRequestMessage req, CancellationToken ct)
    {
        try { return await _http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new RedeSocialException($"Sem ligação ao TikTok: {ex.Message}", temporario: true); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new RedeSocialException("O TikTok demorou demasiado a responder.", temporario: true); }
    }

    private static async Task<JsonElement> Ler(HttpResponseMessage res, CancellationToken ct)
    {
        try { return (await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).Clone(); }
        catch (JsonException) { throw new RedeSocialException($"Resposta inesperada do TikTok ({(int)res.StatusCode}).", temporario: (int)res.StatusCode >= 500); }
    }

    private static JsonElement Dados(JsonElement body, int status)
    {
        if (body.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty("code", out var c) && c.GetString() is { } code && code != "ok")
        {
            var msg = e.TryGetProperty("message", out var m) ? m.GetString() : code;
            var temporario = code is "rate_limit_exceeded" or "internal_error" || status >= 500;
            throw new RedeSocialException($"TikTok: {msg} ({code})", code is "access_token_invalid" ? 190 : null, temporario);
        }
        return body.TryGetProperty("data", out var d) ? d : body;
    }
}

/// <summary>
/// Tokens do TikTok: o de acesso dura 24 h e o de renovação 1 ano. Guardam-se os dois cifrados
/// (JSON) na ligação; antes de publicar renova-se se faltar menos de 10 minutos.
/// </summary>
public sealed class TikTokTokens
{
    private sealed record Par(string A, string R);

    private readonly TikTokClient _tt;
    private readonly ISecretProtector _secrets;
    private readonly ITenantRepository _tenants;

    public TikTokTokens(TikTokClient tt, ISecretProtector secrets, ITenantRepository tenants)
    {
        _tt = tt;
        _secrets = secrets;
        _tenants = tenants;
    }

    public string Cifrar(string access, string refresh) => _secrets.Protect(JsonSerializer.Serialize(new Par(access, refresh)));

    public async Task<string> TokenValidoAsync(RedeSocialLigacao l, CancellationToken ct)
    {
        var par = JsonSerializer.Deserialize<Par>(_secrets.Unprotect(l.TokenCipherText))
                  ?? throw new RedeSocialException("Ligação ao TikTok corrompida — liga outra vez.", 190);
        if (l.TokenExpiraEm is { } exp && exp > DateTime.UtcNow.AddMinutes(10)) return par.A;

        var t = await _tenants.FindByIdAsync(l.TenantId, ct) ?? throw new RedeSocialException("Tenant não encontrado.");
        if (t.TikTokClientKey is null || t.TikTokClientSecretCipherText is null)
            throw new RedeSocialException("Faltam os dados da app TikTok.", 190);
        var r = await _tt.OAuthTokenAsync(new Dictionary<string, string>
        {
            ["client_key"] = t.TikTokClientKey,
            ["client_secret"] = _secrets.Unprotect(t.TikTokClientSecretCipherText),
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = par.R,
        }, ct);
        var access = MetaGraphClient.Str(r, "access_token");
        l.TokenCipherText = Cifrar(access, MetaGraphClient.StrOuNull(r, "refresh_token") ?? par.R);
        l.TokenExpiraEm = DateTime.UtcNow.AddSeconds(long.TryParse(MetaGraphClient.StrOuNull(r, "expires_in"), out var s) ? s : 86400);
        return access;
    }
}

/// <summary>
/// Publicação direta de vídeo (FILE_UPLOAD): creator_info → init → PUT do ficheiro → estado até
/// PUBLISH_COMPLETE. A privacidade é a que o dono escolheu em Definições, se o TikTok a permitir
/// para esta conta (apps ainda não auditadas só podem publicar em privado — SELF_ONLY).
/// </summary>
public sealed class TikTokPublicador : IPublicadorRede
{
    private readonly TikTokClient _tt;
    public TikTokPublicador(TikTokClient tt) => _tt = tt;
    public RedeSocial Rede => RedeSocial.TikTok;

    public async Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
    {
        if (p.Formato != ConteudoFormato.Reel || p.AbrirVideo is null)
            throw new RedeSocialException("No TikTok só se publicam vídeos automaticamente.");

        var info = await _tt.PostJsonAsync("/post/publish/creator_info/query/", p.Token, new { }, ct);
        var opcoes = info.TryGetProperty("privacy_level_options", out var o) ? o.EnumerateArray().Select(x => x.GetString()).ToList() : new();
        var privacidade = p.Privacidade is { } pref && opcoes.Contains(pref) ? pref
            : opcoes.Contains("SELF_ONLY") ? "SELF_ONLY" : opcoes.FirstOrDefault()
            ?? throw new RedeSocialException("O TikTok não devolveu opções de privacidade.");

        byte[] video;
        await using (var s = await p.AbrirVideo(ct))
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms, ct);
            video = ms.ToArray();
        }
        if (video.Length > 64L * 1024 * 1024)
            throw new RedeSocialException("O vídeo tem mais de 64 MB — encurta-o para publicar no TikTok.");

        var titulo = p.Legenda is { Length: > 2200 } l ? l[..2200] : p.Legenda ?? "";
        var init = await _tt.PostJsonAsync("/post/publish/video/init/", p.Token, new
        {
            post_info = new { title = titulo, privacy_level = privacidade, disable_comment = false, disable_duet = false, disable_stitch = false },
            source_info = new { source = "FILE_UPLOAD", video_size = video.Length, chunk_size = video.Length, total_chunk_count = 1 },
        }, ct);
        var publishId = MetaGraphClient.Str(init, "publish_id");
        await _tt.EnviarVideoAsync(MetaGraphClient.Str(init, "upload_url"), video, ct);

        var fim = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            var st = await _tt.PostJsonAsync("/post/publish/status/fetch/", p.Token, new { publish_id = publishId }, ct);
            var estado = MetaGraphClient.StrOuNull(st, "status") ?? "";
            if (estado is "PUBLISH_COMPLETE" or "SEND_TO_USER_INBOX")
            {
                var postId = st.TryGetProperty("publicaly_available_post_id", out var ids) && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() > 0
                    ? ids[0].ToString() : null;
                return new Publicado(postId ?? publishId, null);
            }
            if (estado == "FAILED")
                throw new RedeSocialException($"O TikTok não publicou: {MetaGraphClient.StrOuNull(st, "fail_reason") ?? "sem motivo"}.");
            // O vídeo já foi entregue: repetir criaria um post duplicado. Fica dado como enviado.
            if (DateTime.UtcNow > fim) return new Publicado(publishId, null);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
