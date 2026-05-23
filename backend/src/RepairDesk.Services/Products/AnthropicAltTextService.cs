using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Products;

/// <summary>
/// Sprint 166a (revisto): gera **pacote SEO completo** por produto via Claude Vision.
///
/// Insight Bruno: um iPhone 17 é sempre um iPhone 17 — não vale a pena gerar alt
/// individual por foto. Melhor: 1 chamada por produto que devolve:
/// - SeoTitle (max 60 chars, otimizado para Google)
/// - SeoDescription (max 160 chars, meta description)
/// - Alt (genérico para todas as fotos do produto)
/// - DescriptionMarkdown (descrição completa, 2-3 parágrafos, para a página)
///
/// Custo ~0.5¢ por produto. 100 produtos = 50 cêntimos para SEO completo do catálogo.
/// </summary>
public interface IProductSeoGenerator
{
    /// <summary>
    /// 1 chamada por produto: SEO meta (title + description + markdown). Alt aqui é
    /// genérico — usar GenerateAltAsync para alts específicos por imagem (recomendado SEO).
    /// </summary>
    Task<ProductSeoPack?> GenerateAsync(
        ProductSeoInput input,
        byte[]? sampleImageBytes,
        string? sampleImageMime,
        CancellationToken ct = default);

    /// <summary>
    /// 1 chamada por imagem: alt específico que descreve EXACTAMENTE o que se vê
    /// (ângulo, parte do produto, detalhes). Alts diferentes por foto aumentam SEO
    /// Google Images. Custo ~0.5¢/foto.
    /// </summary>
    Task<string?> GenerateAltAsync(
        byte[] imageBytes,
        string mimeType,
        ProductSeoInput productContext,
        CancellationToken ct = default);
}

public sealed record ProductSeoInput(
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    string? Condition,
    string? ExtraContext);

public sealed record ProductSeoPack(
    string SeoTitle,
    string SeoDescription,
    string Alt,
    string DescriptionMarkdown);

public sealed class AnthropicAltTextService : IProductSeoGenerator
{
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicAltTextService> _logger;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILlmQuotaService _quota;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly string? _centralApiKey;
    private readonly string _model;

    // Sprint 196: prompt revisto após feedback ChatGPT — risco de hallucinations + fluff.
    // Regras: ZERO números técnicos inventados (mAh, W, h, MHz, GHz, MP, Hz). Copy genérico
    // ('ideal para…', 'tem este aspecto…') OK. Specs concretas só se 100% certas pelo modelo
    // ser muito conhecido. Em dúvida, omite.
    // Sprint 197b: prompt revisto após Bruno reclamar de fluff genérico. Permite specs REAIS
    // de modelos conhecidos (iPhones, Galaxy S, Pixel) que são públicas e verificáveis. Só omite
    // se modelo for obscuro/customizado.
    private const string SystemPrompt =
        "És copywriter técnico de uma loja portuguesa especializada em smartphones (LopesTech, Viseu).\n" +
        "Não és um gerador de SEO genérico. Vendes a clientes que sabem o que procuram.\n" +
        "\n" +
        "Recebes dados (marca, modelo, storage, cor, CONDITION) e opcionalmente uma imagem.\n" +
        "Adapta linguagem à CONDITION (NÃO assumas refurbished se for novo):\n" +
        "  - 'Novo (selado)' → novo selado, na caixa original\n" +
        "  - 'Usado original A++/A+/A' → usado original (sem reparação), grade visível\n" +
        "  - 'Recondicionado A/B/C' → recondicionado, foi reparado e testado\n" +
        "  - 'Open Box' → caixa aberta mas não utilizado\n" +
        "\n" +
        "REGRAS DE CONTEÚDO:\n" +
        "1. Para modelos populares e bem documentados (iPhones, Samsung Galaxy S/A, Pixel, etc),\n" +
        "   PODES e DEVES mencionar features reais e específicas (USB-C, MagSafe, Dynamic Island,\n" +
        "   ProMotion, S-Pen, Face ID/Touch ID, eSIM, etc) — são públicas e verificáveis.\n" +
        "2. Para modelos OBSCUROS ou variantes regionais (X-99 Pro Indonesia variant), OMITE specs\n" +
        "   técnicas. Em dúvida, NÃO inventes.\n" +
        "3. NÃO uses fluff genérico: 'desempenho excepcional', 'qualidade premium', 'experiência\n" +
        "   incomparável', 'topo de gama'. Google penaliza desde Helpful Content Update.\n" +
        "4. Linguagem útil e técnica: 'compatível com eSIM', 'ecrã ProMotion', 'USB-C 3.2', 'Face ID'.\n" +
        "5. Estrutura 'para quem é': fotografia, gaming, multitasking, uso profissional, uso comum.\n" +
        "6. Inclui menção da garantia legal PT 36 meses (DL 84/2021) para particulares.\n" +
        "\n" +
        "JSON com 4 campos:\n" +
        "{\n" +
        "  \"seoTitle\": \"max 60 chars, com '| LopesTech' se couber. Marca+modelo+storage+cor\",\n" +
        "  \"seoDescription\": \"max 160 chars. Marca+modelo+CONDITION+benefício-chave+CTA.\",\n" +
        "  \"alt\": \"max 120 chars. Descreve a imagem objectivamente (ângulo, cor, parte visível).\",\n" +
        "  \"descriptionMarkdown\": \"3-4 parágrafos pt-PT. Estrutura: (a) hero 2-3 linhas com features-chave do modelo (apenas se modelo conhecido), (b) 'Para quem é' com 3-4 perfis, (c) menção da CONDITION e garantia legal PT, (d) bullet list final 5-7 itens com features verificáveis específicas do modelo (ex: 'Apple Intelligence ready', 'eSIM + nano-SIM', 'MagSafe', 'USB-C 3.2'). NÃO inventes números que não conheces (bateria mAh, watts, taxa refresh) — só usa se tens certeza absoluta.\"\n" +
        "}\n" +
        "Importante: pt-PT (NÃO Brasil). Sem markdown em seoTitle/seoDescription/alt. Sem emojis.\n" +
        "Sem prefixos 'Compre já!' / 'Aproveite!'. Tom calmo e factual, como Apple ou Backmarket.";

    public AnthropicAltTextService(
        HttpClient http,
        IConfiguration config,
        ILlmUsageTracker tracker,
        ILlmQuotaService quota,
        ITenantContext tenant,
        ITenantRepository tenants,
        ISecretProtector secrets,
        ILogger<AnthropicAltTextService> logger)
    {
        _http = http;
        _tracker = tracker;
        _quota = quota;
        _tenant = tenant;
        _tenants = tenants;
        _secrets = secrets;
        _logger = logger;
        _centralApiKey = config["ANTHROPIC_API_KEY"];
        _model = config["ANTHROPIC_MODEL"] ?? "claude-haiku-4-5-20251001";
    }

    public async Task<ProductSeoPack?> GenerateAsync(
        ProductSeoInput input,
        byte[]? sampleImageBytes,
        string? sampleImageMime,
        CancellationToken ct = default)
    {
        var (apiKey, isByok) = await ResolveApiKeyAsync(ct);
        if (apiKey is null) return null;

        if (!isByok)
        {
            var quotaCheck = await _quota.CheckAsync(ct);
            if (!quotaCheck.Allowed) return null;
        }

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            // Construir contexto textual.
            var contextLines = new List<string>
            {
                $"Marca: {input.Brand}",
                $"Modelo: {input.Model}",
            };
            if (!string.IsNullOrWhiteSpace(input.Storage)) contextLines.Add($"Armazenamento: {input.Storage}");
            if (!string.IsNullOrWhiteSpace(input.Color)) contextLines.Add($"Cor: {input.Color}");
            if (!string.IsNullOrWhiteSpace(input.Condition)) contextLines.Add($"Condição: {input.Condition}");
            if (!string.IsNullOrWhiteSpace(input.ExtraContext)) contextLines.Add($"Extra: {input.ExtraContext}");

            var userText = string.Join("\n", contextLines) + "\n\nGera JSON conforme schema.";

            // Construir content: text + opcionalmente image.
            var content = new List<object> { new { type = "text", text = userText } };
            if (sampleImageBytes is { Length: > 0 } && sampleImageBytes.Length <= 5 * 1024 * 1024)
            {
                var mime = sampleImageMime?.ToLowerInvariant() switch
                {
                    "image/jpg" or "image/jpeg" => "image/jpeg",
                    "image/png" => "image/png",
                    "image/webp" => "image/webp",
                    "image/gif" => "image/gif",
                    _ => "image/jpeg",
                };
                var base64 = Convert.ToBase64String(sampleImageBytes);
                content.Insert(0, new
                {
                    type = "image",
                    source = new { type = "base64", media_type = mime, data = base64 },
                });
            }

            var requestBody = new
            {
                model = _model,
                max_tokens = 800,
                temperature = 0.3,
                system = new[]
                {
                    new { type = "text", text = SystemPrompt, cache_control = new { type = "ephemeral" } },
                },
                messages = new[] { new { role = "user", content = content.ToArray() } },
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
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("ProductSeo Anthropic {Status}: {Body}", (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                outcome = "error";
                return null;
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return null; }

            // Extrai JSON da resposta.
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) { outcome = "error"; return null; }
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<ProductSeoRaw>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Alt)) { outcome = "error"; return null; }

            return new ProductSeoPack(
                SeoTitle: (parsed.SeoTitle ?? "").Trim(),
                SeoDescription: (parsed.SeoDescription ?? "").Trim(),
                Alt: parsed.Alt.Trim(),
                DescriptionMarkdown: (parsed.DescriptionMarkdown ?? "").Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "ProductSeo falhou");
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            return null;
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "product-seo",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
        }
    }

    public async Task<string?> GenerateAltAsync(byte[] imageBytes, string mimeType, ProductSeoInput context, CancellationToken ct = default)
    {
        if (imageBytes is null || imageBytes.Length == 0 || imageBytes.Length > 5 * 1024 * 1024) return null;
        var (apiKey, isByok) = await ResolveApiKeyAsync(ct);
        if (apiKey is null) return null;
        if (!isByok)
        {
            var quotaCheck = await _quota.CheckAsync(ct);
            if (!quotaCheck.Allowed) return null;
        }

        var mime = mimeType?.ToLowerInvariant() switch
        {
            "image/jpg" or "image/jpeg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            "image/gif" => "image/gif",
            _ => "image/jpeg",
        };

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            const string altSystemPrompt =
                "És especialista de SEO + acessibilidade pt-PT para e-commerce.\n" +
                "Recebes uma imagem de produto + contexto (marca/modelo/specs) e devolves UMA frase " +
                "descritiva em pt-PT (max 125 chars) para o atributo alt= em HTML.\n" +
                "REGRAS:\n" +
                "- Descreve o que se vê especificamente (ângulo, parte do produto, detalhes visíveis)\n" +
                "- Inclui marca + modelo + cor se visível\n" +
                "- NÃO uses 'imagem de', 'fotografia de', 'foto'\n" +
                "- NÃO uses aspas\n" +
                "- 1 frase só, sem newlines\n" +
                "- Devolve APENAS a frase, sem prefixo nem markdown";

            var ctxLine = $"Marca: {context.Brand} · Modelo: {context.Model}"
                + (string.IsNullOrWhiteSpace(context.Storage) ? "" : $" · {context.Storage}")
                + (string.IsNullOrWhiteSpace(context.Color) ? "" : $" · {context.Color}");

            var base64 = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                model = _model,
                max_tokens = 100,
                temperature = 0.3,
                system = altSystemPrompt,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image", source = new { type = "base64", media_type = mime, data = base64 } },
                            new { type = "text", text = $"Contexto: {ctxLine}\n\nGera alt text PT específico para esta foto." },
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
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Alt Anthropic {Status}: {Body}", (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                outcome = "error";
                return null;
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return null; }
            text = text.Trim().Trim('"').Trim();
            if (text.Length > 200) text = text[..200].TrimEnd() + "…";
            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Generate alt falhou");
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            return null;
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "alt-text",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
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
                catch { /* fallback central */ }
            }
        }
        return (_centralApiKey, false);
    }

    private sealed record ProductSeoRaw(
        [property: JsonPropertyName("seoTitle")] string? SeoTitle,
        [property: JsonPropertyName("seoDescription")] string? SeoDescription,
        [property: JsonPropertyName("alt")] string? Alt,
        [property: JsonPropertyName("descriptionMarkdown")] string? DescriptionMarkdown);

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
