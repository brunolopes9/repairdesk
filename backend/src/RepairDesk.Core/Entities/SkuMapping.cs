using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 157: tabela de mapeamento entre SKUs do fornecedor → entidades internas
/// (Part ou Product). Aprendida automaticamente quando Bruno aprova items em
/// /importacoes — da próxima vez que vier a mesma <c>SupplierSku</c>, o sistema
/// sugere directamente o match sem precisar de fuzzy search.
///
/// Exemplo: Tudo4Mobile fatura "Touch+Display+Frame Samsung Galaxy A15" com SKU=137491.
/// Bruno aprova → mapeia para o Part interno SAM-A15-LCD. Próxima fatura com SKU=137491
/// salta directo para esse Part.
/// </summary>
public class SkuMapping : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Código do fornecedor (slug — match com <see cref="Fornecedor.Code"/>).
    /// Ex: "tudo4mobile", "molano", "utopya", "lcphones".
    /// </summary>
    public required string SupplierCode { get; set; }

    /// <summary>SKU original do fornecedor (ex: "137491", "MLN-IP12-128-BLK"). Case-insensitive lookup.</summary>
    public required string SupplierSku { get; set; }

    /// <summary>
    /// Nome do produto como veio do fornecedor (ex: "Touch+Display Huawei P20 Lite").
    /// Guardado para audit + para alimentar fuzzy matcher de SKUs futuros sem mapping ainda.
    /// </summary>
    public string? SupplierProductName { get; set; }

    /// <summary>Tipo da entidade interna a que aponta — <see cref="SkuMappingTargetType.Part"/> ou Product.</summary>
    public SkuMappingTargetType TargetType { get; set; }

    /// <summary>FK para Part ou Product (qual depende de TargetType).</summary>
    public Guid TargetId { get; set; }

    /// <summary>Confiança no mapping. Manual=Bruno escolheu na UI; Fuzzy=match sugerido automaticamente; Auto=match exacto de SupplierSku.</summary>
    public SkuMappingConfidence Confidence { get; set; } = SkuMappingConfidence.Manual;

    /// <summary>
    /// Quantas vezes este mapping foi usado/confirmado pelo Bruno. Útil para priorizar
    /// quando há mapping ambiguous (mesmo SupplierSku com 2 targets historicamente).
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>Último import que confirmou este mapping. NULL se foi criado manualmente.</summary>
    public Guid? CreatedFromImportId { get; set; }
    public SupplierInvoiceImport? CreatedFromImport { get; set; }

    /// <summary>Notas opcionais Bruno (ex: "T4M alterou nomenclatura em Mar 2026").</summary>
    public string? Notas { get; set; }
}

public enum SkuMappingTargetType
{
    /// <summary>Peça de reparação (LCD, bateria, etc).</summary>
    Part = 0,
    /// <summary>Telemóvel/acessório vendável na loja.</summary>
    Product = 1,
}

public enum SkuMappingConfidence
{
    /// <summary>Bruno escolheu manualmente na UI aprovação.</summary>
    Manual = 0,
    /// <summary>Match exacto de SupplierSku (já existe no mapping table).</summary>
    Auto = 1,
    /// <summary>Match sugerido via fuzzy (Levenshtein/token) — Bruno deve revalidar.</summary>
    Fuzzy = 2,
}
