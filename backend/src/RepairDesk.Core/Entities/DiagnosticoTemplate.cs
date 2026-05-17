using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Template de checklist de diagnóstico, configurável por tenant.
/// Pode ser default-seeded (tenant-less) ou específico de uma tenant.
/// </summary>
public class DiagnosticoTemplate : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; } // "Smartphone padrão", "Bruno — telemóveis"
    public DeviceCategory Categoria { get; set; } = DeviceCategory.Smartphone;
    public bool Activo { get; set; } = true;
    /// <summary>Marca este template como default para a categoria dentro do tenant.</summary>
    public bool IsDefault { get; set; }

    public List<DiagnosticoTemplateItem> Items { get; set; } = new();
}

public class DiagnosticoTemplateItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public DiagnosticoTemplate? Template { get; set; }

    public required string Label { get; set; } // "Ecrã sem fissuras"
    public string? Descricao { get; set; }      // "Verificar com luz frontal e traseira"
    public int Ordem { get; set; }
    /// <summary>Peso na média ponderada (1-10, default 5).</summary>
    public int Peso { get; set; } = 5;
    /// <summary>Categoria interna para agrupar (ex: "Ecrã", "Áudio", "Conectividade").</summary>
    public string? Grupo { get; set; }
}
