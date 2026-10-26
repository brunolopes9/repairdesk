using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 566: artigo da loja — o que se etiqueta, se procura e se vende ao balcão (ex.: "Película Samsung A34").
/// O stock é a soma dos lotes de compra ligados (<see cref="CompraLinha.ArtigoId"/>); ao vender sai do lote
/// mais antigo. A mesma etiqueta serve para todas as compras do mesmo artigo.
/// </summary>
public class Artigo : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Nº sequencial por tenant (dá o SKU e o código interno). Nunca reutilizado.</summary>
    public int Numero { get; set; }

    public required string Nome { get; set; }

    /// <summary>SKU da loja: "LT-00012".</summary>
    public required string Sku { get; set; }

    /// <summary>Código de barras impresso na etiqueta — EAN-13 interno ("20" + nº) ou o EAN do fabricante.</summary>
    public required string CodigoBarras { get; set; }

    /// <summary>Código que já vem na embalagem (opcional) — a pistola também o reconhece.</summary>
    public string? CodigoFabricante { get; set; }

    public string? Categoria { get; set; }

    /// <summary>Preço de venda com IVA. Null = usa o preço calculado do lote (custo + lucro + IVA).</summary>
    public int? PrecoVendaCents { get; set; }

    public string? Localizacao { get; set; }
    public bool Ativo { get; set; } = true;
    public string? Notas { get; set; }

    public List<CompraLinha> Lotes { get; set; } = new();
}
