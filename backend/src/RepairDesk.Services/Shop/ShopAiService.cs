using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Shop;

/// <summary>
/// Sprint 188: bridge entre loja online e Anthropic API — em vez de a loja chamar
/// Anthropic directamente (com a key no Vercel env), passa pelo RepairDesk para
/// single source of truth de credentials + LlmUsage tracking + rate-limit.
/// Spec em Contexto/61-Shop-AI-Bridge-Spec.md.
/// </summary>
public interface IShopAiService
{
    Task<ShopAssistantResult> AskAssistantAsync(string query, CancellationToken ct = default);
    Task<ShopImageSearchResult> SearchImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}

public sealed record ShopAssistantFilters(
    string? SearchQuery,
    string? Category,
    IReadOnlyList<string>? Brand,
    IReadOnlyList<string>? Storage,
    IReadOnlyList<string>? Color,
    int? PriceMin,
    int? PriceMax);

public sealed record ShopAssistantResult(
    bool Ok,
    ShopAssistantFilters? Filters,
    string? Explanation,
    string? Url,
    string? Error);

public sealed record ShopImageSearchResult(
    bool Ok,
    string? SearchQuery,
    string? Brand,
    string? Model,
    string? Category,
    string? Explanation,
    string? Url,
    string? Error);

public sealed class ShopAiService : IShopAiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopAiService> _logger;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILlmQuotaService _quota;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly string? _centralApiKey;
    private readonly string _model;

    private const string AssistantSystemPrompt =
        """
        És o assistant de compras da LopesTech, uma loja portuguesa de telemóveis recondicionados em Viseu.

        Cliente escreve em linguagem natural o que procura. A tua tarefa: extrair filtros estruturados.

        Categorias disponíveis (slug exacto): "phone", "accessory", "audio", "tablet", "laptop", "other"
        Marcas comuns (lowercase): apple, samsung, xiaomi, oppo, huawei, realme, motorola, lopestech, morelio
        Storage típico: "64GB", "128GB", "256GB", "512GB", "1TB"
        Cores típicas: preto, branco, azul, roxo, dourado, rosa, vermelho, verde

        Responde APENAS com JSON no formato { filters: { searchQuery, category, brand[], storage[], color[], priceMin, priceMax }, explanation }.

        Se a pergunta não é sobre produtos da loja (suporte, reparações, garantias, etc), responde com filters: {} e explanation: "Para esse assunto fala connosco no WhatsApp ou contacto@lopestech.pt."

        NUNCA inventes marcas/modelos. Se o cliente menciona algo que não tens a certeza, usa apenas searchQuery.
        """;

    private const string ImageSearchSystemPrompt =
        """
        És o assistant visual da LopesTech, uma loja portuguesa de telemóveis recondicionados.

        Vês uma imagem que o cliente carregou. Identifica:
        1. Categoria do produto (phone, accessory, audio, tablet, laptop, other)
        2. Marca (apple, samsung, xiaomi, oppo, huawei, etc — lowercase)
        3. Modelo específico se conseguires identificar
        4. Cor visível se for relevante

        Responde APENAS com JSON: { searchQuery, category, brand, model, explanation }.

        Se a imagem não for de um produto reconhecível ou for inadequada:
        { searchQuery: "", explanation: "Não consegui identificar um produto na imagem. Tenta uma foto mais nítida do dispositivo." }

        NUNCA inventes marcas.
        """;

    public ShopAiService(
        HttpClient http,
        IConfiguration config,
        ILlmUsageTracker tracker,
        ILlmQuotaService quota,
        ITenantContext tenant,
        ITenantRepository tenants,
        ISecretProtector secrets,
        ILogger<ShopAiService> logger)
    {
        _http = http;
        _logger = logger;
        _tracker = tracker;
        _quota = quota;
        _tenant = tenant;
        _tenants = tenants;
        _secrets = secrets;
        _centralApiKey = config["ANTHROPIC_API_KEY"];
        _model = config["ANTHROPIC_MODEL"] ?? "claude-haiku-4-5-20251001";
    }

    private async Task<(string? Key, bool IsByok)> ResolveApiKeyAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is { } tenantId)
        {
            var tenantEntity = await _tenants.FindByIdAsync(tenantId, ct);
            if (tenantEntity?.AnthropicApiKeyCipherText is { } cipher)
            {
                try { return (_secrets.Unprotect(cipher), true); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ShopAi: falhou unprotect BYOK Anthropic key tenant={Tid} — fallback central.", tenantId);
                }
            }
        }
        return (_centralApiKey, false);
    }

    public async Task<ShopAssistantResult> AskAssistantAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new(false, null, null, null, "Query vazia");
        query = query.Trim();
        if (query.Length < 2) return new(false, null, null, null, "Query muito curta");
        if (query.Length > 500) return new(false, null, null, null, "Query muito longa (máx 500 chars)");

        var (apiKey, isByok) = await ResolveApiKeyAsync(ct);
        if (apiKey is null)
            return new(false, null, null, null, "ANTHROPIC_API_KEY não configurada");

        if (!isByok)
        {
            var quotaCheck = await _quota.CheckAsync(ct);
            if (!quotaCheck.Allowed)
                return new(false, null, null, null, "Quota IA esgotada para este mês");
        }

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var requestBody = new
            {
                model = _model,
                max_tokens = 512,
                temperature = 0,
                system = new[]
                {
                    new { type = "text", text = AssistantSystemPrompt, cache_control = new { type = "ephemeral" } },
                },
                messages = new[]
                {
                    new { role = "user", content = query },
                },
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(requestBody),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                outcome = "error";
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("ShopAi assistant {Status}: {Body}", (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                return new(false, null, null, null, "Erro Anthropic");
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return new(false, null, null, null, "Resposta vazia"); }

            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) { outcome = "error"; return new(false, null, null, null, "JSON inválido"); }
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<AssistantRawResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null) { outcome = "error"; return new(false, null, null, null, "Parse falhou"); }

            var filters = new ShopAssistantFilters(
                SearchQuery: parsed.Filters?.SearchQuery,
                Category: parsed.Filters?.Category,
                Brand: (IReadOnlyList<string>?)parsed.Filters?.Brand ?? Array.Empty<string>(),
                Storage: (IReadOnlyList<string>?)parsed.Filters?.Storage ?? Array.Empty<string>(),
                Color: (IReadOnlyList<string>?)parsed.Filters?.Color ?? Array.Empty<string>(),
                PriceMin: parsed.Filters?.PriceMin,
                PriceMax: parsed.Filters?.PriceMax);

            return new(true, filters, parsed.Explanation, BuildUrl(filters), null);
        }
        catch (Exception ex)
        {
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            _logger.LogWarning(ex, "ShopAi assistant falhou.");
            return new(false, null, null, null, outcome == "timeout" ? "Timeout" : "Erro interno");
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "shop-assistant",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
        }
    }

    public async Task<ShopImageSearchResult> SearchImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return new(false, null, null, null, null, null, null, "Imagem vazia");
        if (imageBytes.Length > 5 * 1024 * 1024)
            return new(false, null, null, null, null, null, null, "Imagem excede 5MB");

        var (apiKey, isByok) = await ResolveApiKeyAsync(ct);
        if (apiKey is null)
            return new(false, null, null, null, null, null, null, "ANTHROPIC_API_KEY não configurada");

        if (!isByok)
        {
            var quotaCheck = await _quota.CheckAsync(ct);
            if (!quotaCheck.Allowed)
                return new(false, null, null, null, null, null, null, "Quota IA esgotada para este mês");
        }

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(25));

            var base64 = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                model = _model,
                max_tokens = 512,
                temperature = 0,
                system = ImageSearchSystemPrompt,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image", source = new { type = "base64", media_type = mimeType, data = base64 } },
                            new { type = "text", text = "Identifica o produto na imagem. Devolve JSON conforme schema." },
                        },
                    },
                },
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(requestBody),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                outcome = "error";
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("ShopAi image-search {Status}: {Body}", (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                return new(false, null, null, null, null, null, null, "Erro Anthropic");
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return new(false, null, null, null, null, null, null, "Resposta vazia"); }

            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) { outcome = "error"; return new(false, null, null, null, null, null, null, "JSON inválido"); }
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<ImageSearchRawResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null) { outcome = "error"; return new(false, null, null, null, null, null, null, "Parse falhou"); }

            var url = string.IsNullOrWhiteSpace(parsed.Brand)
                ? $"/loja?q={Uri.EscapeDataString(parsed.SearchQuery ?? "")}"
                : $"/loja/{parsed.Brand}?q={Uri.EscapeDataString(parsed.SearchQuery ?? "")}";

            return new(true, parsed.SearchQuery, parsed.Brand, parsed.Model, parsed.Category, parsed.Explanation, url, null);
        }
        catch (Exception ex)
        {
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            _logger.LogWarning(ex, "ShopAi image-search falhou.");
            return new(false, null, null, null, null, null, null, outcome == "timeout" ? "Timeout" : "Erro interno");
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "shop-image-search",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
        }
    }

    private static string BuildUrl(ShopAssistantFilters f)
    {
        var path = string.IsNullOrWhiteSpace(f.Brand?.FirstOrDefault()) ? "/loja" : $"/loja/{f.Brand![0]}";
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.SearchQuery)) qs.Add($"q={Uri.EscapeDataString(f.SearchQuery)}");
        if (f.Storage is { Count: > 0 }) qs.Add($"attr.storage={Uri.EscapeDataString(f.Storage[0])}");
        if (f.Color is { Count: > 0 }) qs.Add($"attr.color={Uri.EscapeDataString(f.Color[0])}");
        if (f.PriceMax.HasValue) qs.Add($"attr.priceMax={f.PriceMax.Value * 100}");
        if (f.PriceMin.HasValue) qs.Add($"attr.priceMin={f.PriceMin.Value * 100}");
        return qs.Count == 0 ? path : $"{path}?{string.Join("&", qs)}";
    }

    private sealed class AssistantRawResponse
    {
        [JsonPropertyName("filters")] public AssistantRawFilters? Filters { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }
    private sealed class AssistantRawFilters
    {
        [JsonPropertyName("searchQuery")] public string? SearchQuery { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("brand")] public List<string>? Brand { get; set; }
        [JsonPropertyName("storage")] public List<string>? Storage { get; set; }
        [JsonPropertyName("color")] public List<string>? Color { get; set; }
        [JsonPropertyName("priceMin")] public int? PriceMin { get; set; }
        [JsonPropertyName("priceMax")] public int? PriceMax { get; set; }
    }
    private sealed class ImageSearchRawResponse
    {
        [JsonPropertyName("searchQuery")] public string? SearchQuery { get; set; }
        [JsonPropertyName("brand")] public string? Brand { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content,
        [property: JsonPropertyName("usage")] AnthropicUsage? Usage);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens,
        [property: JsonPropertyName("cache_creation_input_tokens")] int? CacheCreationInputTokens,
        [property: JsonPropertyName("cache_read_input_tokens")] int? CacheReadInputTokens);
}
