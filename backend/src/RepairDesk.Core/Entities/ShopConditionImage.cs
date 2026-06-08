using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 531: imagem ilustrativa por estado de condição (A+/A/B+/B) para o seletor visual da loja
/// online (estilo Swappie — mostra o desgaste típico de cada grau). 1 imagem por grau, por tenant.
/// O Mender é single source of truth — a loja não gere conteúdo; consome via /api/external/condition-images.
/// </summary>
public class ShopConditionImage : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Grau de loja em slug: "a-plus" | "a" | "b-plus" | "b" (alinhado com GradeSlug do payload externo).</summary>
    public required string Grade { get; set; }

    /// <summary>URL da imagem original optimizada (WebP). As variantes responsivas são opcionais.</summary>
    public required string Url { get; set; }
    public string? Url480w { get; set; }
    public string? Url1024w { get; set; }
    public string? Url2048w { get; set; }
    public string? BlurDataUrl { get; set; }
    public string? Alt { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
