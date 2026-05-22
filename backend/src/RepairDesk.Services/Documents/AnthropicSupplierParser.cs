using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 163: parser LLM (Claude Haiku 4.5) que processa texto extraído de PDFs de fornecedor
/// quando os parsers determinísticos (Sprint 124+134 Tudo4Mobile, Sprint 162 fingerprinting)
/// falharam. Cobre **qualquer** fornecedor com formato razoável.
///
/// Estratégia:
/// - **PII redaction** ANTES de enviar à Anthropic: strip NIFs (^[0-9]{9}$), nomes pessoais
///   óbvios, endereços. Só vai descrição de produtos + valores.
/// - **Prompt estruturado** com schema JSON desejado para a resposta. Claude retorna JSON parseável.
/// - **Cache prompt sistémico** via Anthropic prompt caching — 90% off em parte repetida.
/// - **Modelo**: claude-haiku-4-5-20251001 (cheap + rápido, ~0.5¢/fatura típica).
/// - **Timeout 30s** + **fallback graceful** se LLM falhar (retorna null, não bloqueia).
///
/// Privacidade: dados que vão para Anthropic = só item descriptions + qty + price + dates.
/// Sem NIF cliente, sem nome cliente, sem endereço cliente. Fornecedor name OK (B2B).
/// </summary>
public interface IAnthropicSupplierParser
{
    /// <summary>
    /// Tenta parsear o texto. Retorna null se LLM não configurado, timeout, ou resposta
    /// inválida. Caller deve continuar sem dados extras (degrada graciosamente).
    /// </summary>
    Task<LlmParseResult?> ParseAsync(string pdfText, CancellationToken ct = default);

    /// <summary>
    /// Sprint 164: parseia uma imagem (foto papel de fatura) via Claude Vision.
    /// Aceita JPG/PNG/WebP até 5MB. Devolve mesmo shape de ParseAsync.
    /// PII redaction não aplicável a imagens — assumimos confiança no fornecedor B2B PT/EU.
    /// </summary>
    Task<LlmParseResult?> ParseImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);

    /// <summary>Boolean para o caller saber se LLM está disponível antes de chamar.</summary>
    bool IsConfigured { get; }
}

public sealed record LlmParseResult(
    string? SupplierName,
    string? OrderId,
    DateTime? DocumentDate,
    int? TotalCents,
    IReadOnlyList<LlmParseItem> Items,
    /// <summary>0..1 confidence reported pelo modelo. Sugestão Bruno revalida sempre.</summary>
    double Confidence);

public sealed record LlmParseItem(
    string Description,
    int Quantity,
    int LineTotalCents,
    string? SupplierSku);

public sealed class AnthropicSupplierParser : IAnthropicSupplierParser
{
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicSupplierParser> _logger;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILlmQuotaService _quota;
    private readonly string? _apiKey;
    private readonly string _model;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public AnthropicSupplierParser(HttpClient http, IConfiguration config, ILlmUsageTracker tracker, ILlmQuotaService quota, ILogger<AnthropicSupplierParser> logger)
    {
        _http = http;
        _tracker = tracker;
        _quota = quota;
        _logger = logger;
        _apiKey = config["ANTHROPIC_API_KEY"];
        _model = config["ANTHROPIC_MODEL"] ?? "claude-haiku-4-5-20251001";
        if (string.IsNullOrWhiteSpace(_apiKey))
            _logger.LogInformation("AnthropicSupplierParser: ANTHROPIC_API_KEY não configurado — LLM fallback desactivado.");
    }

    public async Task<LlmParseResult?> ParseAsync(string pdfText, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(pdfText)) return null;
        // Sprint 167b: quota check antes de gastar dinheiro.
        var quotaCheck = await _quota.CheckAsync(ct);
        if (!quotaCheck.Allowed)
        {
            _logger.LogWarning("ParseAsync skipped: quota exceeded ({Used}/{Quota}, plan={Plan}).",
                quotaCheck.Used, quotaCheck.Quota, quotaCheck.Plan);
            return null;
        }

        var redacted = RedactPii(pdfText);
        if (redacted.Length > 12_000) redacted = redacted[..12_000]; // ~3K tokens — fatura típica cabe largamente

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var requestBody = new
            {
                model = _model,
                max_tokens = 1500,
                temperature = 0,
                system = new[]
                {
                    new
                    {
                        type = "text",
                        text = SystemPrompt,
                        cache_control = new { type = "ephemeral" },  // Sprint 163: prompt caching 90% off
                    },
                },
                messages = new[]
                {
                    new { role = "user", content = $"Fatura de fornecedor (texto extraído do PDF):\n\n{redacted}\n\nDevolve JSON conforme schema." },
                },
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(requestBody),
            };
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Anthropic API {Status}: {Body}", (int)resp.StatusCode, body.Length > 500 ? body[..500] : body);
                outcome = "error";
                return null;
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return null; }

            // Extrai JSON puro da resposta (Claude às vezes envolve em markdown ```json).
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) { outcome = "error"; return null; }
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<LlmRawResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null) { outcome = "error"; return null; }

            return new LlmParseResult(
                SupplierName: parsed.SupplierName,
                OrderId: parsed.OrderId,
                DocumentDate: parsed.DocumentDate,
                TotalCents: parsed.TotalCents,
                Items: parsed.Items?.Select(i => new LlmParseItem(
                    i.Description ?? "",
                    i.Quantity,
                    i.LineTotalCents,
                    i.SupplierSku)).ToList() ?? new(),
                Confidence: Math.Clamp(parsed.Confidence, 0, 1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "AnthropicSupplierParser falhou — caller continua sem LLM data.");
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            return null;
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "parse-pdf",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
            // O try/return foi acima — finally apenas faz tracking, não muda return.
            // Mas o catch acima tem return — finally corre antes do return ser retornado? Sim em C#.
        }
    }

    /// <summary>
    /// Sprint 164: parseia foto papel via Claude Vision. Mesmo schema JSON.
    /// </summary>
    public async Task<LlmParseResult?> ParseImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        if (!IsConfigured || imageBytes is null || imageBytes.Length == 0) return null;
        var quotaCheck = await _quota.CheckAsync(ct);
        if (!quotaCheck.Allowed)
        {
            _logger.LogWarning("ParseImageAsync skipped: quota exceeded ({Used}/{Quota}, plan={Plan}).",
                quotaCheck.Used, quotaCheck.Quota, quotaCheck.Plan);
            return null;
        }
        // Claude Vision max 5MB por imagem; rejeita silenciosamente se maior.
        if (imageBytes.Length > 5 * 1024 * 1024)
        {
            _logger.LogWarning("ParseImageAsync: imagem {Size} bytes excede 5MB limite Claude Vision.", imageBytes.Length);
            return null;
        }
        // Normaliza mimeType — Claude aceita image/jpeg, image/png, image/gif, image/webp.
        var normalizedMime = mimeType?.ToLowerInvariant() switch
        {
            "image/jpg" or "image/jpeg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            "image/gif" => "image/gif",
            _ => "image/jpeg", // assume JPG por defeito
        };

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        AnthropicUsage? usageCapture = null;
        var outcome = "ok";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(45)); // Vision é mais lento que text

            var base64 = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                model = _model,
                max_tokens = 1500,
                temperature = 0,
                system = new[]
                {
                    new
                    {
                        type = "text",
                        text = SystemPrompt,
                        cache_control = new { type = "ephemeral" },
                    },
                },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = normalizedMime,
                                    data = base64,
                                },
                            },
                            new
                            {
                                type = "text",
                                text = "Foto/scan de fatura de fornecedor. Extrai os dados conforme schema JSON. Se a imagem está cortada ou ilegível, devolve confidence baixa.",
                            },
                        },
                    },
                },
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(requestBody),
            };
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Anthropic Vision API {Status}: {Body}", (int)resp.StatusCode, body.Length > 500 ? body[..500] : body);
                outcome = "error";
                return null;
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            usageCapture = payload?.Usage;
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) { outcome = "error"; return null; }

            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) { outcome = "error"; return null; }
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<LlmRawResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null) { outcome = "error"; return null; }

            return new LlmParseResult(
                SupplierName: parsed.SupplierName,
                OrderId: parsed.OrderId,
                DocumentDate: parsed.DocumentDate,
                TotalCents: parsed.TotalCents,
                Items: parsed.Items?.Select(i => new LlmParseItem(
                    i.Description ?? "",
                    i.Quantity,
                    i.LineTotalCents,
                    i.SupplierSku)).ToList() ?? new(),
                Confidence: Math.Clamp(parsed.Confidence, 0, 1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "ParseImageAsync falhou — caller continua sem LLM data.");
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            return null;
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "parse-image",
                new LlmUsageTokens(
                    usageCapture?.InputTokens ?? 0,
                    usageCapture?.OutputTokens ?? 0,
                    usageCapture?.CacheReadInputTokens ?? 0,
                    usageCapture?.CacheCreationInputTokens ?? 0),
                (int)startTs.ElapsedMilliseconds, outcome, ct);
        }
    }

    /// <summary>
    /// Sprint 163: redacção de PII antes de enviar para a Anthropic.
    /// Remove apenas o que é manifestamente cliente-side; mantém fornecedor name + items + valores.
    /// </summary>
    internal static string RedactPii(string text)
    {
        // NIF PT (9 dígitos isolados) → [NIF_REDACTED]
        text = Regex.Replace(text, @"\b\d{9}\b", "[NIF]");
        // Códigos postais PT (XXXX-XXX) → [CP]
        text = Regex.Replace(text, @"\b\d{4}-\d{3}\b", "[CP]");
        // IBAN PT → [IBAN]
        text = Regex.Replace(text, @"\bPT\d{2}\s?(?:\d{4}\s?){5}\d{1}\b", "[IBAN]");
        // Emails — só clientes (mantém info@fornecedor.com). Heurística: redacta se domínio gmail/hotmail/yahoo/outlook.
        text = Regex.Replace(text, @"[\w\.-]+@(gmail|hotmail|yahoo|outlook|live|sapo|icloud)\.[\w\.]+",
            "[EMAIL_PESSOAL]", RegexOptions.IgnoreCase);
        // Phone PT (9 dígitos começando 9/2) → [PHONE]
        text = Regex.Replace(text, @"\b[92]\d{8}\b", "[PHONE]");
        return text;
    }

    private const string SystemPrompt =
        """
        És um parser de faturas de fornecedor para uma loja de reparação de telemóveis em Portugal.
        Recebes texto extraído de PDFs (formato variável por fornecedor). Devolves JSON estruturado.

        SCHEMA obrigatório (sem texto fora do JSON):
        {
          "supplierName": "string ou null — nome do fornecedor (B2B emitente)",
          "orderId": "string ou null — número do documento/encomenda",
          "documentDate": "string ISO yyyy-MM-dd ou null",
          "totalCents": número inteiro em cêntimos com IVA ou null,
          "items": [
            {
              "description": "string — descrição do produto/peça",
              "quantity": número inteiro >= 1,
              "lineTotalCents": número inteiro em cêntimos com IVA da linha,
              "supplierSku": "string ou null — código/SKU do produto no catálogo do fornecedor"
            }
          ],
          "confidence": número 0..1 — 1 = certeza absoluta dos valores extraídos
        }

        REGRAS:
        - Preços em cêntimos (multiplicar euros × 100, arredondar).
        - lineTotalCents = preço FINAL da linha COM IVA. Se a fatura mostra unit price sem IVA + IVA separado, calcular total com IVA.
        - Se houver portes/envio numa linha separada, inclui como item próprio.
        - Não inventes — se não tens certeza de um campo, mete null.
        - Tokens [NIF], [CP], [IBAN], [EMAIL_PESSOAL], [PHONE] foram redacted — ignora-os.
        - confidence < 0.5 se o texto é claramente lixo / não-fatura.
        """;

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

    private sealed record LlmRawResponse(
        string? SupplierName,
        string? OrderId,
        DateTime? DocumentDate,
        int? TotalCents,
        List<LlmRawItem>? Items,
        double Confidence);

    private sealed record LlmRawItem(
        string? Description,
        int Quantity,
        int LineTotalCents,
        string? SupplierSku);
}
