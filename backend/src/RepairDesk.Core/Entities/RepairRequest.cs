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

    /// <summary>Quando convertido em reparação, aponta para a Reparacao criada.</summary>
    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>
    /// Sprint 437 (Doc 91 follow-up): quando convertido em orçamento (em vez de
    /// reparação), aponta para o Trabalho criado. Mutuamente exclusivo com
    /// ReparacaoId na prática — staff escolhe um caminho ou outro.
    /// </summary>
    public Guid? TrabalhoId { get; set; }
    public Trabalho? Trabalho { get; set; }

    /// <summary>Motivo da rejeição (opcional) — para histórico interno.</summary>
    public string? MotivoRejeicao { get; set; }

    /// <summary>IP de origem (truncado) — anti-abuso, não PII forte.</summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// Sprint 436 (Doc 91 follow-up Codex): notas internas do staff durante triagem.
    /// Não visíveis ao cliente. Útil para "cliente já ligou", "espera confirmação preço", etc.
    /// </summary>
    public string? NotasInternas { get; set; }

    /// <summary>Sprint 436: prioridade na inbox (default Normal). Para triagem visual.</summary>
    public RepairRequestPrioridade Prioridade { get; set; } = RepairRequestPrioridade.Normal;
}
