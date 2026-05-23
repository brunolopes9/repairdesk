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
    /// <summary>Sprint 197: deprecated, mantido por back-compat. Origin+Grade são o source of truth.
    /// Setter automático ao gravar (ProductGradingMapper.ComposeLegacy). UI nova não usa.</summary>
    public ProductGrading Grading { get; set; } = ProductGrading.Novo;
    /// <summary>Sprint 197: eixo "de onde vem" (New/Used/Refurbished). Independente do estado visual.</summary>
    public ProductOrigin Origin { get; set; } = ProductOrigin.New;
    /// <summary>Sprint 197: estado visual/funcional (Sealed/A++/A+/A/B/C). Sealed só faz sentido com Origin=New.</summary>
    public ProductGrade Grade { get; set; } = ProductGrade.Sealed;
    public ProductSupplyType SupplyType { get; set; } = ProductSupplyType.Stock;
    /// <summary>
    /// Sprint 151: categoria de produto na loja online. Distingue telemóveis (Phone) de
    /// acessórios (Accessory) — capas, vidros, cabos. Permite à loja filtrar/listar com
    /// semântica clara sem misturar smartphones 600€ com capas 5€.
    /// </summary>
    public ProductCategory Category { get; set; } = ProductCategory.Phone;
    /// <summary>
    /// Sprint 151: SKU do fornecedor (para reconciliação CSV Molano e similares). Nullable —
    /// produtos próprios não têm. Indexar (FornecedorId, DropshipSupplierSku) único para
    /// dedupe no importer.
    /// </summary>
    public string? DropshipSupplierSku { get; set; }

    /// <summary>Preço de venda final ao cliente (sem IVA na loja online — IVA aplica-se em checkout).</summary>
    public int PriceCents { get; set; }
    /// <summary>
    /// Sprint 151: preço antes de promoção, para strike-through na PDP loja
    /// (mostra "39,90€ ~~49,90€~~"). Nullable se não há promoção.
    /// </summary>
    public int? CompareAtPriceCents { get; set; }
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

    /// <summary>
    /// Sprint 151: razão pela qual o produto é Open Box ("Devolução cliente, embalagem aberta",
    /// "Demonstração loja", etc). Aparece na PDP loja para transparência. Só relevante quando
    /// <see cref="IsOpenBox"/> == true; ignorado para outros.
    /// </summary>
    public string? OpenBoxReason { get; set; }

    /// <summary>
    /// Sprint 205: flag explícita open-box. Confirmado com shop Claude — Open Box NÃO é 4ª origin,
    /// é boolean dentro de Used (origin=used + grade=A++ + isOpenBox=true). Loja usa para badge
    /// laranja e página /loja/open-box. Permite distinguir "exposição loja" (true) de "usado
    /// premium do cliente que trouxe em condições óptimas" (false).
    /// </summary>
    public bool IsOpenBox { get; set; } = false;

    /// <summary>
    /// Sprint 204: saúde da bateria em % (0-100). Apple iPhone/iPad/Mac mostra no settings.
    /// Null para Samsung/Xiaomi/acessórios (não há indicador padrão). Loja usa para filtros
    /// "Bateria 100% / 95+ / 90+ / 85+" e mostrar selo verde na PDP.
    /// </summary>
    public int? BatteryHealthPercent { get; set; }

    /// <summary>
    /// Sprint 204: estado técnico — Unknown/NeverOpened/OriginalParts/Repaired.
    /// Eixo independente do estético (Grade). Loja usa para selo "Peças originais" + filtros.
    /// </summary>
    public ProductTechnicalState TechnicalState { get; set; } = ProductTechnicalState.Unknown;

    /// <summary>
    /// Sprint 204: notas técnicas free-form para Bruno descrever a intervenção quando TechnicalState=Repaired.
    /// Ex: "Ecrã substituído por original Apple · Bateria nova premium · Face ID funcional".
    /// Aparece na PDP loja como bullet list. NULL ou vazio = não mostra secção.
    /// </summary>
    public string? TechnicalNotes { get; set; }

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
    /// <summary>URL original (raw) — mantida para histórico mesmo após optimização.</summary>
    public required string Url { get; set; }
    public string? Alt { get; set; }
    public int Ordem { get; set; }
    /// <summary>
    /// Sprint 151: true = Bruno verificou/editou esta imagem para a loja (recortou, ajustou).
    /// false = imagem raw vinda do importer CSV Molano. Webhook expõe só curadas se existirem;
    /// fallback às raw quando não há nenhuma curada. Default true (uploads manuais).
    /// </summary>
    public bool IsCurated { get; set; } = true;

    // Sprint 189: pipeline SEO — versões resized + AVIF + blur LQIP (Contexto/60).
    /// <summary>WebP 480w (mobile). Gerado por ImageOptimizationService.</summary>
    public string? Url480w { get; set; }
    /// <summary>WebP 1024w (tablet).</summary>
    public string? Url1024w { get; set; }
    /// <summary>WebP 2048w (desktop).</summary>
    public string? Url2048w { get; set; }
    /// <summary>AVIF 480w (-30% vs WebP).</summary>
    public string? AvifUrl480w { get; set; }
    /// <summary>AVIF 1024w.</summary>
    public string? AvifUrl1024w { get; set; }
    /// <summary>AVIF 2048w.</summary>
    public string? AvifUrl2048w { get; set; }
    /// <summary>LQIP base64 (~2KB) — placeholder blur enquanto carrega.</summary>
    public string? BlurDataUrl { get; set; }
    /// <summary>Width × Height da original — útil para aspect ratio CSS.</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>Quando o pipeline correu — NULL se ainda não foi optimizada (legacy ou upload novo).</summary>
    public DateTime? OptimizedAt { get; set; }
}
