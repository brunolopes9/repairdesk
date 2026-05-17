using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Execução de diagnóstico ligada a uma reparação. Cada reparação tem,
/// no máximo, uma execução (1:1).
/// </summary>
public class DiagnosticoExecucao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public Guid? TemplateId { get; set; }
    public DiagnosticoTemplate? Template { get; set; }
    /// <summary>Cópia do nome do template no momento da execução (caso seja apagado depois).</summary>
    public string? TemplateNomeSnapshot { get; set; }

    public DeviceCategory Categoria { get; set; } = DeviceCategory.Smartphone;
    public DateTime? CompletadoEm { get; set; }
    public string? NotasGerais { get; set; }

    /// <summary>Score 0-100 calculado. Null se ainda não foi computado.</summary>
    public int? Score { get; set; }

    public List<DiagnosticoExecucaoItem> Items { get; set; } = new();
}

public class DiagnosticoExecucaoItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ExecucaoId { get; set; }
    public DiagnosticoExecucao? Execucao { get; set; }

    /// <summary>Snapshot dos campos do TemplateItem (label, peso, ordem) — assim mantemos histórico mesmo se template muda.</summary>
    public required string Label { get; set; }
    public string? Descricao { get; set; }
    public string? Grupo { get; set; }
    public int Ordem { get; set; }
    public int Peso { get; set; } = 5;

    public DiagnosticoResultado Resultado { get; set; } = DiagnosticoResultado.NaoTestado;
    public string? Notas { get; set; }
}
