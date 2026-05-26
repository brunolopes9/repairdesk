using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 354 (Doc 83 Pillar 9): pedido de reparação submetido pelo cliente via
/// widget público no website da loja. É um "lead" — fica Pendente até o staff
/// o converter numa <see cref="Reparacao"/> ou rejeitar.
/// </summary>
public class RepairRequest : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public required string Nome { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public required string Equipamento { get; set; }
    public required string Descricao { get; set; }

    public RepairRequestEstado Estado { get; set; } = RepairRequestEstado.Pendente;

    /// <summary>Quando convertido, aponta para a reparação criada.</summary>
    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>Motivo da rejeição (opcional) — para histórico interno.</summary>
    public string? MotivoRejeicao { get; set; }

    /// <summary>IP de origem (truncado) — anti-abuso, não PII forte.</summary>
    public string? SourceIp { get; set; }
}
