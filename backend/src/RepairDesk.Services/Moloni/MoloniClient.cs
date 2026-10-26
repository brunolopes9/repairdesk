using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Moloni;

/// <summary>Erro devolvido pelo Moloni ON (validação, permissões, sessão) — a mensagem é a do Moloni (HTTP 400).</summary>
public sealed class MoloniException : DomainException
{
    public MoloniException(string message) : base("moloni", "Moloni ON: " + message) { }
}

public interface IMoloniClient
{
    /// <summary>
    /// Corre uma query/mutation e devolve o <c>data</c> da operação <paramref name="operacao"/>. Os erros vêm em
    /// <c>data.{operacao}.errors</c> (validação) ou no topo (sessão) — ambos viram <see cref="MoloniException"/>.
    /// </summary>
    Task<JsonNode?> ExecutarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default);

    /// <summary>Como <see cref="ExecutarAsync"/> mas para operações que devolvem um escalar (ex.: <c>invoiceGetPDF</c> → true).</summary>
    Task<JsonNode?> ExecutarEscalarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default);

    Task<byte[]> DescarregarAsync(string path, string token, CancellationToken ct = default);
}

/// <summary>
/// Sprint 567: cliente GraphQL do Moloni ON (https://api.molonion.pt/v1, "Authorization: Bearer apik:…").
/// Sem estado: a chave vem de quem chama (é por tenant, guardada encriptada).
/// </summary>
public class MoloniClient : IMoloniClient
{
    public const string Endpoint = "https://api.molonion.pt/v1";
    public const string MediaBase = "https://mediaapi.moloni.org";

    private readonly HttpClient _http;
    public MoloniClient(HttpClient http) => _http = http;

    public async Task<JsonNode?> ExecutarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default)
    {
        var resultado = await ExecutarEscalarAsync(apiKey, query, variaveis, operacao, ct);
        if (resultado is not JsonObject obj) return resultado;
        if (obj["errors"] is JsonArray erros && erros.Count > 0)
            throw new MoloniException(string.Join(" | ", erros.Select(e =>
                (e?["field"]?.GetValue<string>() is { } f && f != "*" ? f + ": " : "") + e?["msg"]?.GetValue<string>())));
        return obj["data"];
    }

    public async Task<JsonNode?> ExecutarEscalarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new { query, variables = variaveis }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new MoloniException("A chave da API do Moloni ON não é válida ou expirou.");
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        JsonNode? json;
        try { json = JsonNode.Parse(corpo); }
        catch (JsonException) { throw new MoloniException($"Resposta inesperada do Moloni ON (HTTP {(int)resp.StatusCode})."); }
        if (json?["errors"] is JsonArray topo && topo.Count > 0)
            throw new MoloniException(string.Join(" | ", topo.Select(e => e?["message"]?.GetValue<string>())));
        return json?["data"]?[operacao];
    }

    public async Task<byte[]> DescarregarAsync(string path, string token, CancellationToken ct = default)
    {
        var url = $"{MediaBase}{path}?jwt={Uri.EscapeDataString(token)}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) throw new MoloniException($"Não foi possível descarregar o PDF do Moloni ON (HTTP {(int)resp.StatusCode}).");
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}
