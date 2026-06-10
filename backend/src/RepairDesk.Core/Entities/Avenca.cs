using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 546 (Doc 93 #1): avença — faturação recorrente a um cliente (mensalidade de manutenção
/// de website, contrato de software, etc). A avença é uma FÁBRICA de Trabalhos: cada emissão cria
/// um Trabalho "{Descricao} — MM/yyyy" e emite a Fatura (FT) via Moloni pelo pipeline existente —
/// entra direto no ciclo dívida→push→recibo já live. Modo conservador: o cron NÃO emite sozinho,
/// só avisa (push 1-clique); a emissão automática fica para quando o Bruno confiar.
/// </summary>
public class Avenca : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    /// <summary>Ex.: "Manutenção website Pátio Fidalgo". Vai para o título do Trabalho e da fatura.</summary>
    public required string Descricao { get; set; }

    /// <summary>Valor COM IVA por período (consistente com o resto do Mender).</summary>
    public int ValorCents { get; set; }

    /// <summary>Taxa de IVA da emissão (passa como vatPercent ao provider). Default 23.</summary>
    public decimal IvaRate { get; set; } = 23m;

    public JobCategory Categoria { get; set; } = JobCategory.Software;

    /// <summary>1=mensal, 3=trimestral, 12=anual (clamp 1..24).</summary>
    public int PeriodicidadeMeses { get; set; } = 1;

    /// <summary>Data (UTC, só a parte de dia interessa) em que a próxima emissão fica devida.</summary>
    public DateTime ProximaEmissao { get; set; }

    public bool Ativa { get; set; } = true;
    public string? Notas { get; set; }

    // Rasto da última emissão (o Trabalho criado tem o resto: FT, recibo, etc).
    public DateTime? UltimaEmissaoEm { get; set; }
    public Guid? UltimoTrabalhoId { get; set; }
}
