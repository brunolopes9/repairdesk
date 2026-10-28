using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 581: um código que leva a um artigo, além do código de barras principal.
/// <list type="bullet">
/// <item>Com <see cref="FornecedorId"/>: o SKU desse fornecedor ("iPartsTrade RIX03253 → Película premium"). As compras
/// seguintes com o mesmo SKU do mesmo fornecedor ligam-se sozinhas ao artigo. O SKU só é único dentro do fornecedor.</item>
/// <item>Sem fornecedor: código de barras alternativo — ex.: a etiqueta de um artigo que foi junto noutro continua a
/// funcionar na pistola.</item>
/// </list>
/// </summary>
public class ArtigoCodigo : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid ArtigoId { get; set; }
    public Artigo? Artigo { get; set; }

    public Guid? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Normalizado: maiúsculas, sem espaços nas pontas.</summary>
    public required string Codigo { get; set; }
}
