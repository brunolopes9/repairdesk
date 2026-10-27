using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 570: categoria de artigos da loja com preço de venda por defeito (ex.: "Películas" a 15 €).
/// Um artigo com preço próprio ignora-o; sem preço próprio usa o da categoria; sem nenhum usa o calculado do lote.
/// Liga-se ao artigo pelo nome (<see cref="Artigo.Categoria"/>, sem distinguir maiúsculas).
/// </summary>
public class CategoriaArtigo : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    /// <summary>Preço de venda com IVA para os artigos desta categoria (null = sem preço por defeito).</summary>
    public int? PrecoVendaCents { get; set; }
}
