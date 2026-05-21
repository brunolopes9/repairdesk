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
    private readonly string? _apiKey;
    private readonly string _model;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public AnthropicSupplierParser(HttpClient http, IConfiguration config, ILogger<AnthropicSupplierParser> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["ANTHROPIC_API_KEY"];
        _model = config["ANTHROPIC_MODEL"] ?? "claude-haiku-4-5-20251001";
        if (string.IsNullOrWhiteSpace(_apiKey))
            _logger.LogInformation("AnthropicSupplierParser: ANTHROPIC_API_KEY não configurado — LLM fallback desactivado.");
    }

    public async Task<LlmParseResult?> ParseAsync(string pdfText, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(pdfText)) return null;

        var redacted = RedactPii(pdfText);
        if (redacted.Length > 12_000) redacted = redacted[..12_000]; // ~3K tokens — fatura típica cabe largamente

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
                return null;
            }

            var payload = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(cts.Token);
            var text = payload?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Extrai JSON puro da resposta (Claude às vezes envolve em markdown ```json).
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart) return null;
            var json = text[jsonStart..(jsonEnd + 1)];

            var parsed = JsonSerializer.Deserialize<LlmRawResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (parsed is null) return null;

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
            return null;
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

    private sealed record AnthropicResponse([property: JsonPropertyName("content")] List<AnthropicContent>? Content);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

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
