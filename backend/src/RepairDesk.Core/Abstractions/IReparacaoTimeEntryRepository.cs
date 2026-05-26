using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IReparacaoTimeEntryRepository
{
    /// <summary>Devolve o time entry actualmente activo (EndedAt null) deste user, se existir.</summary>
    Task<ReparacaoTimeEntry?> FindActiveForUserAsync(Guid userId, CancellationToken ct = default);
    Task<ReparacaoTimeEntry?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReparacaoTimeEntry>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(ReparacaoTimeEntry entry, CancellationToken ct = default);
    Task UpdateAsync(ReparacaoTimeEntry entry, CancellationToken ct = default);
    Task DeleteAsync(ReparacaoTimeEntry entry, CancellationToken ct = default);

    /// <summary>Soma de minutos por reparação (apenas entries fechadas).</summary>
    Task<int> SumMinutesByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);

    /// <summary>Estatísticas por user no intervalo [from, to] (UTC).</summary>
    Task<IReadOnlyList<TimeStatsRow>> StatsByUserAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed record TimeStatsRow(Guid UserId, int TotalMinutos, int Sessoes, int Reparacoes);
