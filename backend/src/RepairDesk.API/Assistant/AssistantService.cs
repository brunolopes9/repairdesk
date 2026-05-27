using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Assistant;

public sealed record AssistantMessage(string Role, string Content);
public sealed record AssistantAskRequest(string Question, List<AssistantMessage>? History);
public sealed record AssistantAnswer(string Answer);

public interface IAssistantService
{
    Task<AssistantAnswer> AskAsync(AssistantAskRequest request, CancellationToken ct = default);
    /// <summary>Exposto para teste: executa uma tool de LEITURA e devolve JSON. Nunca escreve.</summary>
    Task<string> RunToolAsync(string name, JsonElement input, CancellationToken ct = default);
}

/// <summary>
/// Sprint 369: assistente interno do Mender. Staff pergunta em linguagem natural ("quanto
/// stock de ecrãs Samsung?", "report deste mês de reparações") e o Claude responde, usando
/// TOOLS EXCLUSIVAMENTE DE LEITURA, scoped ao tenant pelos global query filters do EF.
///
/// Garantia de segurança (pedido explícito do Bruno): NÃO existe nenhuma tool de escrita.
/// O modelo não consegue criar/alterar/apagar nada — por construção, só pode chamar as 3
/// queries read-only abaixo. Reutiliza o padrão Anthropic do AnthropicSupplierParser
/// (key per-tenant BYOK→central, usage tracking, quota, prompt caching).
/// </summary>
public sealed class AssistantService : IAssistantService
{
    private readonly HttpClient _http;
    private readonly ILlmUsageTracker _tracker;
    private readonly ILlmQuotaService _quota;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly AppDbContext _db;
    private readonly ILogger<AssistantService> _logger;
    private readonly string? _centralApiKey;
    private readonly string _model;

    public AssistantService(
        HttpClient http,
        Microsoft.Extensions.Configuration.IConfiguration config,
        ILlmUsageTracker tracker,
        ILlmQuotaService quota,
        ITenantContext tenant,
        ITenantRepository tenants,
        ISecretProtector secrets,
        AppDbContext db,
        ILogger<AssistantService> logger)
    {
        _http = http;
        _tracker = tracker;
        _quota = quota;
        _tenant = tenant;
        _tenants = tenants;
        _secrets = secrets;
        _db = db;
        _logger = logger;
        _centralApiKey = config["ANTHROPIC_API_KEY"];
        // Sonnet: melhor raciocínio para perguntas livres (o parser usa Haiku).
        _model = config["ASSISTANT_MODEL"] ?? "claude-sonnet-4-6";
    }

    private async Task<(string? Key, bool IsByok)> ResolveApiKeyAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is { } tenantId)
        {
            var tenantEntity = await _tenants.FindByIdAsync(tenantId, ct);
            if (tenantEntity?.AnthropicApiKeyCipherText is { } cipher)
            {
                try { return (_secrets.Unprotect(cipher), true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Unprotect BYOK falhou — fallback central."); }
            }
        }
        return (_centralApiKey, false);
    }

    // ---- Tools (read-only) -------------------------------------------------

    private static readonly object[] Tools =
    {
        new
        {
            name = "search_stock",
            description = "Pesquisa peças/stock por texto (nome, marca ou modelo). Devolve quantidade em stock e mínimo. Usar para perguntas tipo 'quanto stock há de X'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Texto a procurar no nome/marca/modelo (ex: 'ecrã Samsung A52')" },
                    low_stock_only = new { type = "boolean", description = "Se true, só peças abaixo do mínimo" },
                },
            },
        },
        new
        {
            name = "repairs_report",
            description = "Relatório de reparações num período. Conta por estado e total. Filtros opcionais por estado e por texto do equipamento (ex: 'Samsung', 'ecrã').",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    days = new { type = "integer", description = "Janela em dias a contar de hoje (default 30)" },
                    estado = new { type = "string", description = "Filtra por estado: Recebido, Diagnostico, AguardaPeca, EmReparacao, Pronto, Entregue, Cancelado, Orcamento" },
                    equipamento_contains = new { type = "string", description = "Filtra reparações cujo equipamento contém este texto (ex: 'Samsung')" },
                },
            },
        },
        new
        {
            name = "sales_report",
            description = "Relatório de vendas pagas num período: número de vendas e total faturado (com IVA).",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    days = new { type = "integer", description = "Janela em dias a contar de hoje (default 30)" },
                },
            },
        },
        new
        {
            name = "search_clientes",
            description = "Procura clientes por nome, telefone, email ou NIF. Devolve contactos e nota importante. Usar para perguntas sobre um cliente específico.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Texto a procurar (nome, telefone, email ou NIF)" },
                },
                required = new[] { "query" },
            },
        },
        new
        {
            name = "search_reparacoes",
            description = "Lista reparações com detalhe (cliente, equipamento, avaria, estado, orçamento/preço final). Filtros opcionais por texto e estado. Usar para 'que reparações...', 'estado da reparação do X'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Texto a procurar no equipamento, avaria ou nome do cliente" },
                    estado = new { type = "string", description = "Recebido, Diagnostico, AguardaPeca, EmReparacao, Pronto, Entregue, Cancelado, Orcamento" },
                    days = new { type = "integer", description = "Só reparações criadas nos últimos N dias (opcional)" },
                },
            },
        },
        new
        {
            name = "search_vendas",
            description = "Lista vendas recentes (número, data, total, estado, origem, cliente). Para perguntas sobre vendas concretas.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    days = new { type = "integer", description = "Janela em dias (default 30)" },
                },
            },
        },
        new
        {
            name = "despesas_report",
            description = "Total de despesas por categoria num período. Para perguntas sobre custos/despesas.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    days = new { type = "integer", description = "Janela em dias (default 30)" },
                },
            },
        },
    };

    public async Task<string> RunToolAsync(string name, JsonElement input, CancellationToken ct = default)
    {
        switch (name)
        {
            case "search_stock":
            {
                var query = GetString(input, "query");
                var lowOnly = GetBool(input, "low_stock_only");
                var q = _db.Parts.AsNoTracking().Where(p => p.Activo);
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var term = query.Trim();
                    q = q.Where(p => p.Nome.Contains(term)
                        || (p.Marca != null && p.Marca.Contains(term))
                        || (p.Modelo != null && p.Modelo.Contains(term)));
                }
                if (lowOnly == true)
                    q = q.Where(p => p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima);

                var items = await q.OrderBy(p => p.QtdStock).Take(25)
                    .Select(p => new { p.Nome, p.Marca, p.Modelo, p.QtdStock, p.QtdMinima })
                    .ToListAsync(ct);
                return JsonSerializer.Serialize(new { count = items.Count, items });
            }
            case "repairs_report":
            {
                var days = GetInt(input, "days") ?? 30;
                var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
                var q = _db.Reparacoes.AsNoTracking().Where(r => r.CreatedAt >= since);

                var estadoStr = GetString(input, "estado");
                if (!string.IsNullOrWhiteSpace(estadoStr) && Enum.TryParse<RepairStatus>(estadoStr, true, out var estado))
                    q = q.Where(r => r.Estado == estado);

                var contains = GetString(input, "equipamento_contains");
                if (!string.IsNullOrWhiteSpace(contains))
                {
                    var term = contains.Trim();
                    q = q.Where(r => r.Equipamento.Contains(term));
                }

                var porEstado = await q.GroupBy(r => r.Estado)
                    .Select(g => new { estado = g.Key.ToString(), count = g.Count() })
                    .ToListAsync(ct);
                var total = porEstado.Sum(x => x.count);
                return JsonSerializer.Serialize(new { periodo_dias = days, total, por_estado = porEstado });
            }
            case "sales_report":
            {
                var days = GetInt(input, "days") ?? 30;
                var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
                var vendas = _db.Vendas.AsNoTracking()
                    .Where(v => v.Status == VendaStatus.Paga && v.CreatedAt >= since);
                var count = await vendas.CountAsync(ct);
                var totalCents = count == 0 ? 0 : await vendas.SumAsync(v => v.TotalCents, ct);
                var ivaCents = count == 0 ? 0 : await vendas.SumAsync(v => v.IvaCents, ct);
                return JsonSerializer.Serialize(new
                {
                    periodo_dias = days,
                    numero_vendas = count,
                    total_euros = Math.Round(totalCents / 100.0, 2),
                    iva_euros = Math.Round(ivaCents / 100.0, 2),
                });
            }
            case "search_clientes":
            {
                var query = GetString(input, "query")?.Trim();
                if (string.IsNullOrWhiteSpace(query))
                    return JsonSerializer.Serialize(new { error = "indica um nome, telefone, email ou NIF" });
                var clientes = await _db.Clientes.AsNoTracking()
                    .Where(c => c.Nome.Contains(query)
                        || (c.Telefone != null && c.Telefone.Contains(query))
                        || (c.Email != null && c.Email.Contains(query))
                        || (c.Nif != null && c.Nif.Contains(query)))
                    .OrderBy(c => c.Nome).Take(20)
                    .Select(c => new
                    {
                        c.Id, c.Nome, c.Telefone, c.Email, c.Nif, c.NotaImportante,
                        reparacoes = _db.Reparacoes.Count(r => r.ClienteId == c.Id),
                    })
                    .ToListAsync(ct);
                return JsonSerializer.Serialize(new { count = clientes.Count, clientes });
            }
            case "search_reparacoes":
            {
                var q = _db.Reparacoes.AsNoTracking().AsQueryable();
                var days = GetInt(input, "days");
                if (days is { } d)
                    q = q.Where(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-Math.Clamp(d, 1, 366)));
                var estadoStr = GetString(input, "estado");
                if (!string.IsNullOrWhiteSpace(estadoStr) && Enum.TryParse<RepairStatus>(estadoStr, true, out var estado))
                    q = q.Where(r => r.Estado == estado);
                var query = GetString(input, "query")?.Trim();
                if (!string.IsNullOrWhiteSpace(query))
                    q = q.Where(r => r.Equipamento.Contains(query) || r.Avaria.Contains(query)
                        || (r.Cliente != null && r.Cliente.Nome.Contains(query)));

                var reps = await q.OrderByDescending(r => r.CreatedAt).Take(20)
                    .Select(r => new
                    {
                        cliente = r.Cliente != null ? r.Cliente.Nome : null,
                        r.Equipamento,
                        r.Avaria,
                        estado = r.Estado.ToString(),
                        orcamento_euros = r.OrcamentoCents != null ? Math.Round(r.OrcamentoCents.Value / 100.0, 2) : (double?)null,
                        preco_final_euros = r.PrecoFinalCents != null ? Math.Round(r.PrecoFinalCents.Value / 100.0, 2) : (double?)null,
                        criada = r.CreatedAt.ToString("yyyy-MM-dd"),
                    })
                    .ToListAsync(ct);
                return JsonSerializer.Serialize(new { count = reps.Count, reparacoes = reps });
            }
            case "search_vendas":
            {
                var days = GetInt(input, "days") ?? 30;
                var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
                var vendas = await (
                    from v in _db.Vendas.AsNoTracking().Where(v => v.Data >= since)
                    join c in _db.Clientes.AsNoTracking() on v.ClienteId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    orderby v.Data descending
                    select new
                    {
                        v.Numero,
                        data = v.Data.ToString("yyyy-MM-dd"),
                        total_euros = Math.Round(v.TotalCents / 100.0, 2),
                        estado = v.Status.ToString(),
                        origem = v.Origem.ToString(),
                        cliente = c != null ? c.Nome : null,
                    }).Take(20).ToListAsync(ct);
                return JsonSerializer.Serialize(new { count = vendas.Count, vendas });
            }
            case "despesas_report":
            {
                var days = GetInt(input, "days") ?? 30;
                var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
                var porCategoria = await _db.Despesas.AsNoTracking()
                    .Where(d => d.CreatedAt >= since)
                    .GroupBy(d => d.Categoria)
                    .Select(g => new { categoria = g.Key.ToString(), total_euros = Math.Round(g.Sum(x => x.ValorCents) / 100.0, 2), n = g.Count() })
                    .ToListAsync(ct);
                var totalEuros = porCategoria.Sum(x => x.total_euros);
                return JsonSerializer.Serialize(new { periodo_dias = days, total_euros = totalEuros, por_categoria = porCategoria });
            }
            default:
                return JsonSerializer.Serialize(new { error = $"tool desconhecida: {name}" });
        }
    }

    private static string? GetString(JsonElement o, string p)
        => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool? GetBool(JsonElement o, string p)
        => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(p, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : null;
    private static int? GetInt(JsonElement o, string p)
        => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    // ---- Claude tool-use loop ---------------------------------------------

    public async Task<AssistantAnswer> AskAsync(AssistantAskRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return new AssistantAnswer("Faz-me uma pergunta sobre o teu negócio (stock, reparações, vendas).");

        var (apiKey, isByok) = await ResolveApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AssistantAnswer("O assistente não está configurado (falta a chave de IA). Vê Definições → Uso de IA.");
        if (!isByok)
        {
            var quotaCheck = await _quota.CheckAsync(ct);
            if (!quotaCheck.Allowed)
                return new AssistantAnswer($"Limite de IA atingido para este mês ({quotaCheck.Used}/{quotaCheck.Quota}).");
        }

        // Histórico (texto simples) + pergunta nova.
        var messages = new List<object>();
        foreach (var m in (request.History ?? new()).TakeLast(10))
            messages.Add(new { role = m.Role == "assistant" ? "assistant" : "user", content = m.Content });
        messages.Add(new { role = "user", content = request.Question.Trim() });

        var startTs = System.Diagnostics.Stopwatch.StartNew();
        var inTok = 0; var outTok = 0; var outcome = "ok";
        try
        {
            for (var iteration = 0; iteration < 6; iteration++)
            {
                var body = new
                {
                    model = _model,
                    max_tokens = 1024,
                    system = new[]
                    {
                        new { type = "text", text = SystemPrompt, cache_control = new { type = "ephemeral" } },
                    },
                    tools = Tools,
                    messages,
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = JsonContent.Create(body),
                };
                req.Headers.Add("x-api-key", apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(40));
                using var resp = await _http.SendAsync(req, cts.Token);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogWarning("Assistant Anthropic {Status}: {Body}", (int)resp.StatusCode, err.Length > 400 ? err[..400] : err);
                    outcome = "error";
                    return new AssistantAnswer("Não consegui responder agora. Tenta outra vez daqui a pouco.");
                }

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
                var root = doc.RootElement;
                if (root.TryGetProperty("usage", out var u))
                {
                    inTok += u.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
                    outTok += u.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
                }

                var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;
                var contentBlocks = root.GetProperty("content");

                if (stopReason != "tool_use")
                {
                    // Resposta final: concatena os blocos de texto.
                    var answer = string.Concat(contentBlocks.EnumerateArray()
                        .Where(b => b.GetProperty("type").GetString() == "text")
                        .Select(b => b.GetProperty("text").GetString()));
                    return new AssistantAnswer(string.IsNullOrWhiteSpace(answer)
                        ? "Não encontrei nada para responder a isso."
                        : answer.Trim());
                }

                // Há tool_use: devolve o turno do assistant tal-e-qual + resultados das tools.
                messages.Add(new { role = "assistant", content = JsonSerializer.Deserialize<JsonElement>(contentBlocks.GetRawText()) });
                var toolResults = new List<object>();
                foreach (var block in contentBlocks.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() != "tool_use") continue;
                    var toolName = block.GetProperty("name").GetString() ?? "";
                    var toolId = block.GetProperty("id").GetString() ?? "";
                    var toolInput = block.TryGetProperty("input", out var ti) ? ti : default;
                    string result;
                    try { result = await RunToolAsync(toolName, toolInput, ct); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tool {Tool} falhou", toolName);
                        result = JsonSerializer.Serialize(new { error = "falha a consultar os dados" });
                    }
                    toolResults.Add(new { type = "tool_result", tool_use_id = toolId, content = result });
                }
                messages.Add(new { role = "user", content = toolResults });
            }

            outcome = "max-iterations";
            return new AssistantAnswer("A pergunta ficou complexa demais. Tenta reformular mais simples.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "AssistantService falhou.");
            outcome = ex is TaskCanceledException ? "timeout" : "error";
            return new AssistantAnswer("Demorei demasiado a responder. Tenta uma pergunta mais simples.");
        }
        finally
        {
            startTs.Stop();
            await _tracker.RecordAsync(_model, "assistant",
                new LlmUsageTokens(inTok, outTok, 0, 0), (int)startTs.ElapsedMilliseconds, outcome, ct);
        }
    }

    private const string SystemPrompt =
        """
        És o assistente interno de uma loja de reparação de telemóveis em Portugal, dentro do
        software de gestão Mender. Ajudas o staff com QUALQUER pergunta sobre o negócio deles —
        clientes, reparações, stock/peças, vendas e despesas. Usa as tools para ir buscar dados
        reais antes de responder — nunca inventes números nem dados.

        REGRAS:
        - Responde em português de Portugal, curto e directo. Valores em euros.
        - Usa as tools para responder; podes combinar várias (ex: procurar o cliente e depois as
          reparações dele). Para perguntas amplas, escolhe a tool mais próxima.
        - Só tens acesso de LEITURA aos dados desta loja. NÃO consegues criar, alterar, apagar
          nem mexer em definições/código — se te pedirem uma ação dessas, explica que só consultas
          informação e que a alteração tem de ser feita por uma pessoa na app.
        - Dados de clientes são confidenciais desta loja — usa-os só para responder ao staff.
        - Se uma tool devolve 0 resultados, di-lo claramente em vez de inventar.
        """;
}
