using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 615 (Doc 98): o que aconteceu a um item da rotina num dia (feito, saltado, adiado) e que
/// avisos já saíram (<see cref="Avisos"/>, para cada aviso sair uma única vez). Único por item e dia.
/// </summary>
public class RotinaRegisto : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RotinaItemId { get; set; }
    public RotinaItem? Item { get; set; }
    public DateOnly Dia { get; set; }

    /// <summary>null = ainda nada registado (a linha pode existir só por causa dos avisos).</summary>
    public RotinaRegistoEstado? Estado { get; set; }
    public DateTime? RegistadoEm { get; set; }
    public DateTime? AdiadoAte { get; set; }
    /// <summary>Porquê saltou/adiou (obrigatório para adiar um limite, ex.: fecho do PC).</summary>
    public string? Motivo { get; set; }
    /// <summary>Valor medido (ex.: peso em kg).</summary>
    public decimal? Valor { get; set; }

    /// <summary>Chaves dos avisos já enviados, separadas por «|».</summary>
    public string Avisos { get; set; } = string.Empty;
}
