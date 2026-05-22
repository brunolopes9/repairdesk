using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 167a: tracking de uso da API Anthropic per-tenant. Cada chamada LLM
/// (parse PDF, parse foto, alt text, …) regista linha aqui com tokens consumidos
/// + custo estimado em cêntimos.
///
/// Usado para:
/// - UI /definicoes/llm-usage: Bruno vê quanto gastou este mês
/// - Quota enforcement (Sprint 167c): bloquear chamadas quando ultrapassa plano
/// - Faturação SaaS futura: cobrar overage acima do incluído no plano
///
/// Custo calculado server-side baseado em preços Anthropic conhecidos do modelo
/// no momento da chamada. Snapshot — se preços Anthropic baixarem no futuro,
/// histórico mantém o custo na altura.
/// </summary>
public class LlmUsage : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Modelo Anthropic usado (ex: "claude-haiku-4-5-20251001").</summary>
    public required string Model { get; set; }
    /// <summary>Operação interna (ex: "parse-pdf", "parse-image", "alt-text").</summary>
    public required string Operation { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    /// <summary>Tokens de cache lidos (90% off do input price).</summary>
    public int CacheReadTokens { get; set; }
    /// <summary>Tokens de cache escritos (25% extra sobre input price).</summary>
    public int CacheWriteTokens { get; set; }
    /// <summary>Custo estimado em décimos de cêntimo (10000 = 1€). Precisão sub-cêntimo importa quando 1 chamada custa 0.5¢.</summary>
    public int CostMicrocents { get; set; }
    /// <summary>Latência da chamada (ms) — para debugging.</summary>
    public int LatencyMs { get; set; }
    /// <summary>Resultado: "ok" | "error" | "timeout".</summary>
    public string Outcome { get; set; } = "ok";
}
