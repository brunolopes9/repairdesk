using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RepairDesk.Services.Conteudo.Redes;

/// <summary>Erro devolvido pela Graph API do Facebook/Instagram ou pela API do Threads.</summary>
public sealed class RedeSocialException : Exception
{
    public int? Codigo { get; }
    /// <summary>Token expirado/revogado (código 190) — é preciso ligar a conta outra vez.</summary>
    public bool TokenInvalido => Codigo == 190;
    /// <summary>Erros que costumam passar sozinhos (limites, indisponibilidade, media ainda a processar).</summary>
    public bool Temporario { get; }

    public RedeSocialException(string message, int? codigo = null, bool temporario = false) : base(message)
    {
        Codigo = codigo;
        Temporario = temporario;
    }
}

/// <summary>
/// Sprint 599 (Doc 95 Fase B): HTTP fino para graph.facebook.com e graph.threads.net. Todos os
/// pedidos levam o token como parâmetro de formulário/query (nunca em logs) e os erros viram
/// <see cref="RedeSocialException"/> com a mensagem da própria rede.
/// </summary>
public sealed class MetaGraphClient
{
    private readonly HttpClient _http;
    public string GraphBase { get; }
    public string ThreadsBase { get; }

    public MetaGraphClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var versao = config["Meta:GraphVersion"] ?? "v24.0";
        GraphBase = $"https://graph.facebook.com/{versao}";
        ThreadsBase = "https://graph.threads.net/v1.0";
    }

    public Task<JsonElement> GetAsync(string url, IDictionary<string, string?> query, CancellationToken ct)
        => EnviarAsync(new HttpRequestMessage(HttpMethod.Get, ComQuery(url, query)), ct);

    public Task<JsonElement> PostAsync(string url, IDictionary<string, string?> form, CancellationToken ct, IDictionary<string, string>? headers = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form.Where(kv => kv.Value is not null).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!))),
        };
        if (headers is not null)
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        return EnviarAsync(req, ct);
    }

    private async Task<JsonElement> EnviarAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using (req)
        {
            HttpResponseMessage res;
            try
            {
                res = await _http.SendAsync(req, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new RedeSocialException($"Sem ligação à rede social: {ex.Message}", temporario: true);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new RedeSocialException("A rede social demorou demasiado a responder.", temporario: true);
            }

            using (res)
            {
                JsonElement body;
                try
                {
                    body = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                }
                catch (JsonException)
                {
                    throw new RedeSocialException($"Resposta inesperada da rede ({(int)res.StatusCode}).", temporario: (int)res.StatusCode >= 500);
                }

                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var err))
                {
                    var codigo = err.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : (int?)null;
                    var msg = err.TryGetProperty("error_user_msg", out var um) ? um.GetString()
                        : err.TryGetProperty("message", out var m) ? m.GetString() : null;
                    // 4/17/32/613 = limites de pedidos; 1/2 = erro temporário do lado da Meta; 9007 = media ainda a processar.
                    var temporario = codigo is 1 or 2 or 4 or 17 or 32 or 613 or 9007 || (int)res.StatusCode >= 500;
                    throw new RedeSocialException(msg ?? $"Erro {(int)res.StatusCode} da rede social.", codigo, temporario);
                }
                if (!res.IsSuccessStatusCode)
                    throw new RedeSocialException($"Erro {(int)res.StatusCode} da rede social.", temporario: (int)res.StatusCode >= 500);
                return body.Clone();
            }
        }
    }

    private static string ComQuery(string url, IDictionary<string, string?> query)
    {
        var qs = string.Join('&', query.Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return qs.Length == 0 ? url : $"{url}{(url.Contains('?') ? '&' : '?')}{qs}";
    }

    public static string Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? v.ToString()
            : throw new RedeSocialException($"A rede não devolveu «{prop}».");

    public static string? StrOuNull(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;
}
