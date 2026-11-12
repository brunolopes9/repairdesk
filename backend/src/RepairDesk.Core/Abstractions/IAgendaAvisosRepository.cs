using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 611: consultas de TODAS as empresas para o trabalho de fundo dos avisos da agenda.</summary>
public interface IAgendaAvisosRepository
{
    /// <summary>Agendados/confirmados entre as duas horas e ainda sem lembrete (com tracking, para marcar).</summary>
    Task<IReadOnlyList<Appointment>> SemLembreteEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default);
    /// <summary>Agendados/confirmados entre as duas horas (só leitura), de todas as empresas.</summary>
    Task<IReadOnlyList<Appointment>> AtivosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default);
    /// <summary>Sprint 618: peças ainda por encomendar em reparações em curso, de todas as empresas.</summary>
    Task<IReadOnlyList<PecaPorEncomendar>> PecasPorEncomendarAsync(CancellationToken ct = default);
    /// <summary>Sprint 640: publicações por aprovar marcadas até essa hora (inclui atrasadas), de todas as empresas.</summary>
    Task<IReadOnlyList<ConteudoPorAprovar>> ConteudoPorAprovarAteAsync(DateTime ateUtc, CancellationToken ct = default);
    Task<bool> AvisoJaEnviadoAsync(Guid tenantId, string chave, CancellationToken ct = default);
    Task AddAvisoAsync(AgendaAviso aviso, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Sprint 618: uma peça por encomendar (para o aviso antes do prazo do fornecedor).</summary>
/// <summary>Sprint 640: uma publicação à espera de aprovação (para o aviso da manhã).</summary>
public sealed record ConteudoPorAprovar(Guid TenantId, string Titulo, DateTime AgendadoPara);

public sealed record PecaPorEncomendar(Guid TenantId, int VendaNumero, string? Equipamento, string Descricao, int Quantidade);
