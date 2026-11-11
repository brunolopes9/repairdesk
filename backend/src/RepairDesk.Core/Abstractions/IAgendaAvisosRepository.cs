using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 611: consultas de TODAS as empresas para o trabalho de fundo dos avisos da agenda.</summary>
public interface IAgendaAvisosRepository
{
    /// <summary>Agendados/confirmados entre as duas horas e ainda sem lembrete (com tracking, para marcar).</summary>
    Task<IReadOnlyList<Appointment>> SemLembreteEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default);
    /// <summary>Agendados/confirmados entre as duas horas (só leitura), de todas as empresas.</summary>
    Task<IReadOnlyList<Appointment>> AtivosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default);
    Task<bool> AvisoJaEnviadoAsync(Guid tenantId, string chave, CancellationToken ct = default);
    Task AddAvisoAsync(AgendaAviso aviso, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
