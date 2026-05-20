using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 122: produto vendável na loja online — distinto de <see cref="Part"/>. Telemóveis
/// refurbished (Molano), telemóveis novos (Tudo4Mobile), tablets, etc.
///
/// <para>Decisão de design (acordada com loja online em 2026-05-20):</para>
/// <list type="bullet">
///   <item>Read replica local na loja faz cron sync (1-6h) via <c>/api/external/products</c>.</item>
///   <item>RepairDesk é authoritative para preço, stock, status. Loja só faz browsing.</item>
///   <item>Checkout valida em tempo real (não confia na replica).</item>
/// </list>
/// </summary>
public class Product : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>SKU interno (auto-gerado ou definido — único por tenant).</summary>
    public required string Sku { get; set; }
    /// <summary>Slug URL-friendly (ex: "iphone-13-128gb-azul-grade-a") — único por tenant, indexado para SEO.</summary>
    public required string Slug { get; set; }
    /// <summary>Marca (Apple, Samsung, Xiaomi, etc).</summary>
    public required string Brand { get; set; }
    /// <summary>Modelo (iPhone 13, Galaxy A15, Redmi Note 12).</summary>
    public required string Model { get; set; }
    /// <summary>Armazenamento (128GB, 256GB, 512GB, "N/A" se não aplicável).</summary>
    public string? Storage { get; set; }
    /// <summary>Cor (Black, Blue, Pink).</summary>
    public string? Color { get; set; }
    public ProductGrading Grading { get; set; } = ProductGrading.Novo;
    public ProductSupplyType SupplyType { get; set; } = ProductSupplyType.Stock;

    /// <summary>Preço de venda final ao cliente (sem IVA na loja online — IVA aplica-se em checkout).</summary>
    public int PriceCents { get; set; }
    /// <summary>Stock disponível. Para Dropship é tipicamente um valor alto (ou 0=sem cap) — depende da política.</summary>
    public int StockQuantity { get; set; }
    public int StockMinima { get; set; }

    /// <summary>Custo unitário (preço pago ao fornecedor + transporte). Para margem analytics — NÃO exposto externamente.</summary>
    public int CustoUnitarioCents { get; set; }

    /// <summary>Markdown — descrição rica do produto para a página PDP na loja.</summary>
    public string? DescriptionMarkdown { get; set; }
    /// <summary>Atributos livres em JSON (ex: { "ram": "4GB", "processor": "A15 Bionic" }). Esquema flexível.</summary>
    public string? AttributesJson { get; set; }

    /// <summary>Override SEO title (default: nome derivado de Brand+Model+Storage+Grading).</summary>
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    public bool Active { get; set; } = true;
    /// <summary>Default true (Product foi criado para ir à loja). Bruno desliga para esconder temporariamente.</summary>
    public bool MostrarLojaOnline { get; set; } = true;

    public Guid? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    public List<ProductImage> Images { get; set; } = new();
}

/// <summary>
/// Imagem de produto. Relação 1:N com Product. Order para ordenação na galeria PDP.
/// URLs apontam para storage externo (R2/S3) — RepairDesk não armazena ficheiros aqui.
/// </summary>
public class ProductImage : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Url { get; set; }
    public string? Alt { get; set; }
    public int Ordem { get; set; }
}
