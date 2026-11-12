using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 637 (Doc 98 F8): o que o assistente fez (auditoria) e como se desfaz (durante 5 minutos).
/// </summary>
public class AssistenteAcao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>«tarefa» | «rotina».</summary>
    public required string Tipo { get; set; }
    public Guid EntidadeId { get; set; }
    public string? Dia { get; set; }
    /// <summary>Estado anterior (JSON) para desfazer.</summary>
    public string? Antes { get; set; }
    public required string Resumo { get; set; }
    public DateTime? DesfeitaEm { get; set; }
}
