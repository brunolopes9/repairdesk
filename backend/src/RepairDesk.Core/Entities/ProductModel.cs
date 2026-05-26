using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 359 (Doc 83): template de modelo. Guarda o conteúdo PARTILHADO por todas as
/// unidades do mesmo modelo (ex: Apple · iPhone 15) — descrição, specs, imagens de
/// marketing, preço de bateria nova. As <see cref="Product"/> (unidades) ligam via
/// <see cref="Product.ModelId"/> e herdam estes campos; podem sobrepor (override) caso a
/// caso. Resolve o problema de preencher 50× o mesmo conteúdo ao importar lotes.
///
/// <para>Chave de negócio: (TenantId, Brand, Model) único.</para>
/// </summary>
public class ProductModel : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Marca (Apple, Samsung, …). Parte da chave única com Model.</summary>
    public required string Brand { get; set; }
    /// <summary>Modelo (iPhone 15, Galaxy S24). Parte da chave única com Brand.</summary>
    public required string Model { get; set; }

    /// <summary>Descrição comercial partilhada (Markdown). Unidade herda se não tiver a sua.</summary>
    public string? DescriptionMarkdown { get; set; }

    /// <summary>Especificações técnicas em JSON (ecrã, chip, câmaras…). Esquema flexível.</summary>
    public string? SpecsJson { get; set; }

    /// <summary>
    /// Preço da bateria nova deste modelo, em cêntimos. Definido 1× por modelo.
    /// NULL = modelo sem upgrade de bateria disponível. O shop decide a elegibilidade
    /// (condition≠new && !isOpenBox && bateria&lt;95%) — o Mender só publica o preço.
    /// </summary>
    public int? BatteryUpgradePriceCents { get; set; }

    /// <summary>Categoria/série para o shop agrupar variantes (ex: "iPhone 15", "Galaxy A").</summary>
    public ProductCategory Category { get; set; } = ProductCategory.Phone;
    /// <summary>Série livre opcional (ex: "iPhone 15", "Galaxy S") — agrupamento de marketing.</summary>
    public string? Series { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>Imagens de marketing oficiais do modelo (partilhadas por todas as unidades).</summary>
    public List<ProductModelImage> Images { get; set; } = new();

    /// <summary>Unidades (variantes) ligadas a este modelo.</summary>
    public List<Product> Units { get; set; } = new();
}

/// <summary>
/// Sprint 359: imagem de marketing do modelo (foto "oficial"). Espelha
/// <see cref="ProductImage"/> para reutilizar o pipeline SEO (Sprint 189).
/// </summary>
public class ProductModelImage : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductModelId { get; set; }
    public ProductModel? ProductModel { get; set; }

    public required string Url { get; set; }
    public string? Alt { get; set; }
    public int Ordem { get; set; }

    public string? Url480w { get; set; }
    public string? Url1024w { get; set; }
    public string? Url2048w { get; set; }
    public string? AvifUrl480w { get; set; }
    public string? AvifUrl1024w { get; set; }
    public string? AvifUrl2048w { get; set; }
    public string? BlurDataUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime? OptimizedAt { get; set; }
}
