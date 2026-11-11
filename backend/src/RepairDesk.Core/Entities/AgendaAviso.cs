using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 611: registo de um resumo da agenda já enviado («dia:2026-11-11», «semana:2026-W46»),
/// para cada resumo sair uma única vez por empresa, mesmo com reinícios do servidor.
/// </summary>
public class AgendaAviso : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Chave { get; set; }
    public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;
}
