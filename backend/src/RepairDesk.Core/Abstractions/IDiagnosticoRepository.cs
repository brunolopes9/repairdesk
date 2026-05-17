using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IDiagnosticoRepository
{
    Task<IReadOnlyList<DiagnosticoTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task<DiagnosticoTemplate?> FindTemplateAsync(Guid id, CancellationToken ct = default);
    Task<DiagnosticoTemplate?> FindDefaultTemplateAsync(DeviceCategory cat, CancellationToken ct = default);
    Task AddTemplateAsync(DiagnosticoTemplate template, CancellationToken ct = default);
    void RemoveTemplate(DiagnosticoTemplate template);

    Task<DiagnosticoExecucao?> FindExecucaoByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddExecucaoAsync(DiagnosticoExecucao execucao, CancellationToken ct = default);
    void RemoveExecucao(DiagnosticoExecucao execucao);

    Task SaveAsync(CancellationToken ct = default);
}
