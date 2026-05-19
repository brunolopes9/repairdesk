using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Garantia digital. Pode ter origem em Reparação (auto-emitida ao entregar)
/// OU em Venda (auto-emitida ao marcar paga, ex: refurbished — DL 84/2021).
/// URL pública /g/{slug} permite ao cliente verificar validade sem login.
/// Exactamente um de <see cref="ReparacaoId"/> ou <see cref="VendaId"/> deve estar preenchido.
/// </summary>
public class Garantia : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public Guid? VendaId { get; set; }
    public Venda? Venda { get; set; }

    public GarantiaSourceType SourceType { get; set; } = GarantiaSourceType.Reparacao;

    /// <summary>Slug curto público para URL /g/{slug}. Distinto do PublicSlug da reparação.</summary>
    public required string Slug { get; set; }

    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime DataFim { get; set; }
    public int DiasGarantia { get; set; } = 90;

    /// <summary>Cobertura textual: o que está coberto ("Apenas o serviço prestado e peças substituídas").</summary>
    public string? Cobertura { get; set; }
    public string? Exclusoes { get; set; }

    /// <summary>Anulada manualmente (ex: cliente abriu equipamento).</summary>
    public bool Anulada { get; set; }
    public string? MotivoAnulacao { get; set; }
}
