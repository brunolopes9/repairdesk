using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 167a: regista uso da API Anthropic em LlmUsage. Calcula custo estimado em
/// microcêntimos (1¢ = 1000 µ¢) baseado em preços oficiais por modelo.
///
/// Preços Anthropic (Maio 2026, snapshot):
///   claude-haiku-4-5 : $1/MTok input, $5/MTok output, cache read 90% off, cache write +25%
///   claude-sonnet-4-6: $3/MTok input, $15/MTok output, mesmos cache rates
///   claude-opus-4-7  : $15/MTok input, $75/MTok output, mesmos cache rates
/// </summary>
public interface ILlmUsageTracker
{
    Task RecordAsync(string model, string operation, LlmUsageTokens tokens, int latencyMs, string outcome, CancellationToken ct = default);
}

public sealed record LlmUsageTokens(
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens,
    int CacheWriteTokens);

public sealed class LlmUsageTracker : ILlmUsageTracker
{
    private readonly ILlmUsageRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LlmUsageTracker> _logger;

    public LlmUsageTracker(ILlmUsageRepository repo, ITenantContext tenant, ILogger<LlmUsageTracker> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task RecordAsync(string model, string operation, LlmUsageTokens tokens, int latencyMs, string outcome, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            _logger.LogWarning("LlmUsageTracker: tenant context ausente — skipping record (op={Op}).", operation);
            return;
        }
        try
        {
            var cost = CalculateCostMicrocents(model, tokens);
            await _repo.AddAsync(new LlmUsage
            {
                TenantId = tenantId,
                Model = model,
                Operation = operation,
                InputTokens = tokens.InputTokens,
                OutputTokens = tokens.OutputTokens,
                CacheReadTokens = tokens.CacheReadTokens,
                CacheWriteTokens = tokens.CacheWriteTokens,
                CostMicrocents = cost,
                LatencyMs = latencyMs,
                Outcome = outcome,
            }, ct);
            await _repo.SaveAsync(ct);
            _logger.LogInformation("LLM usage: op={Op} model={Model} input={I} output={O} cost=€{Cost:F4}",
                operation, model, tokens.InputTokens, tokens.OutputTokens, cost / 100000.0);
        }
        catch (Exception ex)
        {
            // Tracking não pode bloquear o fluxo principal — log e continua.
            _logger.LogError(ex, "LlmUsageTracker falhou — chamada LLM já completou, só perdemos tracking.");
        }
    }

    /// <summary>
    /// Calcula custo em microcêntimos (1¢ = 1000 µ¢) com base nos preços Anthropic do modelo.
    /// Microcêntimos porque uma chamada típica custa ~500 µ¢ (0.5 cêntimos) — int sub-cent matters.
    /// </summary>
    internal static int CalculateCostMicrocents(string model, LlmUsageTokens tokens)
    {
        // Preços por milhão de tokens em microcêntimos.
        // $1/MTok = 100¢/MTok = 100_000_000 µ¢/MTok = 100 µ¢/KTok = 0.1 µ¢/token.
        var (inputRate, outputRate) = ResolveRates(model);
        // Cache: read = 10% do input; write = 125% do input (Anthropic prompt caching).
        var cacheReadRate = inputRate * 0.10;
        var cacheWriteRate = inputRate * 1.25;

        var costMicrocents =
            tokens.InputTokens * inputRate
            + tokens.OutputTokens * outputRate
            + tokens.CacheReadTokens * cacheReadRate
            + tokens.CacheWriteTokens * cacheWriteRate;
        return (int)Math.Round(costMicrocents);
    }

    /// <summary>(inputRate µ¢/token, outputRate µ¢/token) baseado no modelo Anthropic.</summary>
    private static (double Input, double Output) ResolveRates(string model)
    {
        // Default: Haiku 4.5 ($1 input / $5 output por MTok = 0.1/0.5 µ¢/token).
        if (model.Contains("haiku", StringComparison.OrdinalIgnoreCase)) return (0.1, 0.5);
        if (model.Contains("sonnet", StringComparison.OrdinalIgnoreCase)) return (0.3, 1.5);
        if (model.Contains("opus", StringComparison.OrdinalIgnoreCase)) return (1.5, 7.5);
        // Modelo desconhecido — assume Sonnet (conservador, paga-se mais).
        return (0.3, 1.5);
    }
}
