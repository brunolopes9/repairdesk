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

    /// <summary>Doc 94 Fase 4: quando convertido numa Venda de reparação (orçamento), aponta para ela.</summary>
    public Guid? VendaId { get; set; }
    public Venda? Venda { get; set; }


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

    /// <summary>
    /// Sprint 448 (ROAPP lead deadlines): data/hora em que o staff deve voltar
    /// a contactar este lead. NULL = sem follow-up marcado.
    /// </summary>
    public DateTime? FollowUpAt { get; set; }

    /// <summary>
    /// Sprint 438 (Doc 91 follow-up): canal de entrada. Widget é o default
    /// (pedidos vindos do widget público). Outros valores são usados quando
    /// o staff regista manualmente um lead que veio por telefone, email, etc.
    /// </summary>
    public RepairRequestOrigem Origem { get; set; } = RepairRequestOrigem.Widget;
}
