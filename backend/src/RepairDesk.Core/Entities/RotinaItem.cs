using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 615 (Doc 98): um bloco/hábito recorrente da rotina de um utilizador (refeição, meditação,
/// ginásio, trading, fecho do PC…). É pessoal: cada utilizador só vê e recebe avisos dos seus.
/// Repete-se nos dias da semana de <see cref="Dias"/> (bit = 1 &lt;&lt; DayOfWeek) ou, se
/// <see cref="IntervaloDias"/> estiver definido, de N em N dias a contar de <see cref="DataInicio"/>.
/// </summary>
public class RotinaItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public required string Titulo { get; set; }
    /// <summary>Uma linha por passo (ex.: alimentos da refeição) — aparece como lista no ecrã e no aviso.</summary>
    public string? Detalhe { get; set; }
    public RotinaTipo Tipo { get; set; }

    /// <summary>Hora de início (hora de Lisboa).</summary>
    public TimeOnly Hora { get; set; }
    /// <summary>0 = momento (ex.: refeição); &gt;0 = bloco com fim.</summary>
    public int DuracaoMin { get; set; }

    public int Dias { get; set; }
    public int? IntervaloDias { get; set; }
    public DateOnly? DataInicio { get; set; }

    /// <summary>Minutos antes do início em que avisa (CSV, ex.: «15,5»). Vazio = só à hora.</summary>
    public string? AvisosAntes { get; set; }
    /// <summary>Se não for registado, volta a avisar aos +15 e +30 min.</summary>
    public bool Insistir { get; set; }
    /// <summary>Avisa quando o bloco acaba (para não passar do tempo).</summary>
    public bool AvisarFim { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Sprint 635: alimentos da refeição (só para itens do tipo Refeição).</summary>
    public List<RotinaAlimento> Alimentos { get; set; } = new();
}
