using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IVisitasSiteRepository
{
    Task AdicionarAsync(IEnumerable<EventoSite> eventos, CancellationToken ct = default);
    /// <summary>Eventos da empresa atual no período (mais antigos primeiro), no máximo <paramref name="max"/>.</summary>
    Task<IReadOnlyList<EventoSite>> DoPeriodoAsync(DateTime deUtc, DateTime ateUtc, int max, CancellationToken ct = default);
    Task<IReadOnlyList<EventoSite>> DaSessaoAsync(string sessao, CancellationToken ct = default);
    /// <summary>Apaga tudo o que um visitante fez (empresa atual) — «fui eu, não contar».</summary>
    Task<int> ApagarDoVisitanteAsync(string visitante, CancellationToken ct = default);
    /// <summary>Todas as empresas — retenção.</summary>
    Task<int> ApagarAntesDeAsync(DateTime antesUtc, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
