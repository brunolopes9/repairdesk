using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Products;

/// <summary>
/// Sprint 203: detecta automaticamente mapeamento entre colunas CSV e campos canónicos
/// (sku/brand/model/price/etc) via Claude Haiku. Bruno faz upload CSV de fornecedor novo
/// e Claude analisa header + 2 amostras. Bruno confirma 1× e mapping fica em
/// Fornecedor.CsvColumnMappingJson — próximos uploads automáticos.
/// </summary>
public interface ICsvColumnDetector
{
    Task<CsvColumnMapping?> DetectAsync(string[] header, IReadOnlyList<string[]> sampleRows, CancellationToken ct = default);
}

public sealed record CsvColumnMapping(
    string? Sku,
    string? Brand,
    string? Model,
    string? Product,
    string? Storage,
    string? Color,
    string? Grading,
    string? Price,
    string? Stock,
    string? Cost,
    string? Images,
    string Confidence,
    string Notes);

public sealed class CsvColumnDetectionService : ICsvColumnDetector
{
    private readonly HttpClient _http;
    private readonly ILogger<CsvColumnDetectionService> _logger;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILlmQuotaService _quota;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly string? _centralApiKey;
    private readonly string _model;

    private const string SystemPrompt =
        "Mapeia colunas CSV de fornecedores de telemóveis para um schema canónico.\n" +
        "Recebes header (lista de nomes de colunas) e 1-3 amostras de linhas.\n" +
        "Devolves JSON com as colunas canónicas mapeadas para nomes EXACTOS do header.\n" +
        "\n" +
        "Schema canónico:\n" +
        "  - sku: identificador único (ex: 'SKU', 'reference', 'supplier_sku', 'ref')\n" +
        "  - brand: marca separada (ex: 'Brand', 'Marca'). \"\" se não há coluna separada.\n" +
        "  - model: modelo separado (ex: 'Model', 'Modelo'). \"\" se vem combinado em 'product'.\n" +
        "  - product: descrição combinada (ex: 'Product', 'Description').\n" +
        "  - storage: capacidade (ex: 'Storage', 'Capacity', 'GB')\n" +
        "  - color: cor (ex: 'Color', 'Colour', 'Cor')\n" +
        "  - grading: grade de estado (ex: 'Grade', 'Grading', 'Condition')\n" +
        "  - price: preço venda (ex: 'Price', 'Price (EUR)', 'PVP'). NÃO escolher 'cost' nem 'warranty price'.\n" +
        "  - stock: stock (ex: 'Stock', 'Qty', 'Quantity')\n" +
        "  - cost: preço custo (diferente de price)\n" +
        "  - images: URLs (ex: 'Images', 'Image URL')\n" +
        "\n" +
        "REGRAS:\n" +
        "1. NUNCA inventes. Só valores EXACTOS do header recebido.\n" +
        "2. Se não há correspondência, null.\n" +
        "3. Para 'price' preferes coluna genérica (não Warranty).\n" +
        "4. Se header tem 'Product' sem 'Brand'/'Model', usa product e deixa brand=\"\"/model=\"\".\n" +
        "5. Confidence: 'high'=todas críticas detectadas; 'medium'=sku+price OK mas faltam algumas; 'low'=incerto.\n" +
        "\n" +
        "Devolve APENAS JSON puro (sem markdown):\n" +
        "{\"sku\":\"SKU\",\"brand\":\"\",\"model\":\"\",\"product\":\"Product\",\"storage\":\"Storage\",\"color\":\"Colour\",\"grading\":\"Grade\",\"price\":\"Price (EUR)\",\"stock\":\"Stock\",\"cost\":null,\"images\":null,\"confidence\":\"high\",\"notes\":\"Brand/Model combinados em Product.\"}";

    public CsvColumnDetectionService(
        HttpClient http,
        IConfiguration config,
        ILogger<CsvColumnDetectionService> logger,
        ILlmUsageTracker tracker,
        ILlmQuotaService quota,
        ITenantContext tenant,
        ITenantRepository tenants,
        ISecretProtector secrets)
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

    public async Task<CsvColumnMapping?> DetectAsync(string[] header, IReadOnlyList<string[]> sampleRows, CancellationToken ct = default)
    {
        if (header.Length == 0) return null;

        var (apiKey, _) = await ResolveApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var quotaCheck = await _quota.CheckAsync(ct);
        if (!quotaCheck.Allowed) return null;

        var userText = $"Header: {JsonSerializer.Serialize(header)}\n" +
                       "Amostras:\n" +
                       string.Join("\n", sampleRows.Take(3).Select((r, i) => $"  Linha {i + 1}: {JsonSerializer.Serialize(r)}"));

        var requestBody = new
        {
            model = _model,
            max_tokens = 500,
            system = SystemPrompt,
            messages = new[] { new { role = "user", content = userText } },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(requestBody),
        };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await _http.SendAsync(req, ct);
            sw.Stop();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("CSV detect: Anthropic returned {Status}", resp.StatusCode);
                return null;
            }

            var anthropic = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken: ct);
            var text = anthropic?.Content?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text)) return null;

            await _tracker.RecordAsync(
                _model, "csv-column-detect",
                new LlmUsageTokens(anthropic!.Usage?.InputTokens ?? 0, anthropic.Usage?.OutputTokens ?? 0, 0, 0),
                (int)sw.ElapsedMilliseconds, "ok", ct);

            var json = text.Trim();
            if (json.StartsWith("```"))
            {
                json = json.Trim('`');
                if (json.StartsWith("json\n")) json = json[5..];
            }

            var raw = JsonSerializer.Deserialize<CsvColumnRaw>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (raw is null) return null;

            // Validação anti-hallucination: só aceitar nomes que existem no header
            var headerSet = new HashSet<string>(header, StringComparer.OrdinalIgnoreCase);
            string? Validate(string? col) => string.IsNullOrEmpty(col) ? col : (headerSet.Contains(col) ? col : null);

            return new CsvColumnMapping(
                Sku: Validate(raw.Sku),
                Brand: Validate(raw.Brand),
                Model: Validate(raw.Model),
                Product: Validate(raw.Product),
                Storage: Validate(raw.Storage),
                Color: Validate(raw.Color),
                Grading: Validate(raw.Grading),
                Price: Validate(raw.Price),
                Stock: Validate(raw.Stock),
                Cost: Validate(raw.Cost),
                Images: Validate(raw.Images),
                Confidence: raw.Confidence ?? "low",
                Notes: raw.Notes ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSV detect: falhou");
            return null;
        }
    }

    private async Task<(string? Key, bool IsByok)> ResolveApiKeyAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is { } tenantId)
        {
            var tenantEntity = await _tenants.FindByIdAsync(tenantId, ct);
            if (tenantEntity?.AnthropicApiKeyCipherText is { } cipher)
            {
                try { return (_secrets.Unprotect(cipher), true); }
                catch { }
            }
        }
        return (_centralApiKey, false);
    }

    private sealed record CsvColumnRaw(
        [property: JsonPropertyName("sku")] string? Sku,
        [property: JsonPropertyName("brand")] string? Brand,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("product")] string? Product,
        [property: JsonPropertyName("storage")] string? Storage,
        [property: JsonPropertyName("color")] string? Color,
        [property: JsonPropertyName("grading")] string? Grading,
        [property: JsonPropertyName("price")] string? Price,
        [property: JsonPropertyName("stock")] string? Stock,
        [property: JsonPropertyName("cost")] string? Cost,
        [property: JsonPropertyName("images")] string? Images,
        [property: JsonPropertyName("confidence")] string? Confidence,
        [property: JsonPropertyName("notes")] string? Notes);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content,
        [property: JsonPropertyName("usage")] AnthropicUsage? Usage);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
