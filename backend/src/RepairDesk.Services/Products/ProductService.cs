using System.Text;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Webhooks;

namespace RepairDesk.Services.Products;

public interface IProductService
{
    Task<PagedResult<ProductDto>> SearchAsync(string? search, string? brand, bool? lojaOnline, bool includeInactive, int page, int pageSize, CancellationToken ct = default);
    Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(ProductWriteRequest req, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, ProductWriteRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sprint 153: importer CSV Molano. Upsert idempotente por (FornecedorId, DropshipSupplierSku).</summary>
    Task<ImportProductsResponse> ImportMolanoCsvAsync(string csv, Guid fornecedorId, CancellationToken ct = default);

    /// <summary>Sprint 203: import CSV usando mapping específico (Bruno confirma após Claude).</summary>
    Task<ImportProductsResponse> ImportCsvWithMappingAsync(
        string csv,
        Guid fornecedorId,
        CsvImportMapping mapping,
        bool saveMapping,
        CancellationToken ct = default);
    /// <summary>
    /// Sprint 155: migra produtos shop-only (existiam só na loja antes do single-source-of-truth)
    /// para o RepairDesk. Upsert por SKU. Todos ficam MostrarLojaOnline=true.
    /// </summary>
    Task<ImportProductsResponse> MigrateShopProductsAsync(IReadOnlyList<MigrateShopProductRequest> products, CancellationToken ct = default);
    /// <summary>Sprint 190: força re-emit do webhook phones.atualizado de um produto (backfill após
    /// optimização imagens). Spec doc 62.</summary>
    Task RepublishWebhookAsync(Guid productId, CancellationToken ct = default);
}

public sealed record MigrateShopProductRequest(
    string Sku,
    string Brand,
    string Model,
    string Title,
    string Category,
    int PriceCents,
    int? CompareAtPriceCents,
    int StockQuantity,
    string? Storage,
    string? Color,
    string? Grading,
    string? Description,
    string? SeoTitle,
    string? SeoDescription,
    IReadOnlyList<string>? Images,
    bool IsOpenBox,
    string? OpenBoxReason,
    bool IsActive);

public sealed record ImportProductsResponse(
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<ImportProductError> Errors);

public sealed record ImportProductError(int Line, string Field, string Message, string? Sku);

/// <summary>Sprint 203: mapping de colunas CSV → campos canónicos (Claude-detected ou manual).</summary>
public sealed record CsvImportMapping(
    string? Sku,
    string? Brand,
    string? Model,
    string? Product,
    string? Storage,
    string? Color,
    string? Grading,
    string? Price,
    string? Stock,
    string? Cost,
    string? Images);

public sealed record ProductImageDto(Guid Id, string Url, string? Alt, int Ordem, bool IsCurated);

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Slug,
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    /// <summary>Sprint 197: deprecated mas mantido (back-compat). Origin+Grade são o canonical.</summary>
    ProductGrading Grading,
    ProductOrigin Origin,
    ProductGrade Grade,
    ProductSupplyType SupplyType,
    // Sprint 151: novos campos shop.
    ProductCategory Category,
    string? DropshipSupplierSku,
    int PriceCents,
    int? CompareAtPriceCents,
    int StockQuantity,
    int StockMinima,
    int CustoUnitarioCents,
    string? DescriptionMarkdown,
    string? AttributesJson,
    string? SeoTitle,
    string? SeoDescription,
    string? OpenBoxReason,
    /// <summary>Sprint 205: flag explícita open-box (Used+A++ pode ser premium do cliente OU exposição loja). Loja usa para badge laranja.</summary>
    bool IsOpenBox,
    /// <summary>Sprint 204: saúde bateria 0-100% (null = não aplicável).</summary>
    int? BatteryHealthPercent,
    /// <summary>Sprint 204: estado técnico (NeverOpened/OriginalParts/Repaired/Unknown).</summary>
    ProductTechnicalState TechnicalState,
    /// <summary>Sprint 204: notas técnicas free-form (mostra na PDP loja se preenchido).</summary>
    string? TechnicalNotes,
    bool Active,
    bool MostrarLojaOnline,
    Guid? FornecedorId,
    string? FornecedorNome,
    string? FornecedorCode,
    IReadOnlyList<ProductImageDto> Images,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ProductImageWriteRequest(string Url, string? Alt, int Ordem, bool IsCurated = true);

public sealed record ProductWriteRequest(
    string? Sku,
    string? Slug,
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    /// <summary>Sprint 197: deprecated. UI nova envia Origin+Grade; Grading é recalculado server-side.</summary>
    ProductGrading Grading,
    ProductOrigin Origin,
    ProductGrade Grade,
    ProductSupplyType SupplyType,
    ProductCategory Category,
    string? DropshipSupplierSku,
    int PriceCents,
    int? CompareAtPriceCents,
    int StockQuantity,
    int StockMinima,
    int CustoUnitarioCents,
    string? DescriptionMarkdown,
    string? AttributesJson,
    string? SeoTitle,
    string? SeoDescription,
    string? OpenBoxReason,
    /// <summary>Sprint 205: flag open-box explícita.</summary>
    bool IsOpenBox,
    /// <summary>Sprint 204: saúde bateria 0-100% (null = sem info).</summary>
    int? BatteryHealthPercent,
    /// <summary>Sprint 204: estado técnico (Unknown default).</summary>
    ProductTechnicalState TechnicalState,
    /// <summary>Sprint 204: notas técnicas free-form (loja mostra na PDP se preenchido).</summary>
    string? TechnicalNotes,
    bool Active,
    bool MostrarLojaOnline,
    Guid? FornecedorId,
    IReadOnlyList<ProductImageWriteRequest>? Images);

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly IWebhookPublisher _webhooks;
    private readonly IFornecedorRepository _fornecedores;

    public ProductService(IProductRepository repo, ITenantContext tenant, IAuditLogger audit, IWebhookPublisher webhooks, IFornecedorRepository fornecedores)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
        _webhooks = webhooks;
        _fornecedores = fornecedores;
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(string? search, string? brand, bool? lojaOnline, bool includeInactive, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _repo.SearchAsync(search, brand, lojaOnline, includeInactive, page, pageSize, ct);
        return new PagedResult<ProductDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Product", id);
        return ToDto(p);
    }

    public async Task<ProductDto> CreateAsync(ProductWriteRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        Validate(req);
        var sku = await EnsureUniqueSkuAsync(req.Sku, null, req.Brand, req.Model, ct);
        var slug = await EnsureUniqueSlugAsync(req.Slug, null, req.Brand, req.Model, req.Storage, req.Color, req.Grading, ct);

        var entity = new Product
        {
            TenantId = tenantId,
            Sku = sku,
            Slug = slug,
            Brand = req.Brand.Trim(),
            Model = req.Model.Trim(),
            Storage = Clean(req.Storage),
            Color = Clean(req.Color),
            Origin = req.Origin,
            Grade = req.Grade,
            // Sprint 197: Grading recalculado server-side a partir do par Origin+Grade.
            Grading = ProductGradingMapper.ComposeLegacy(req.Origin, req.Grade),
            SupplyType = req.SupplyType,
            Category = req.Category,
            DropshipSupplierSku = Clean(req.DropshipSupplierSku),
            PriceCents = req.PriceCents,
            CompareAtPriceCents = req.CompareAtPriceCents,
            StockQuantity = req.StockQuantity,
            StockMinima = req.StockMinima,
            CustoUnitarioCents = req.CustoUnitarioCents,
            DescriptionMarkdown = Clean(req.DescriptionMarkdown),
            AttributesJson = Clean(req.AttributesJson),
            SeoTitle = Clean(req.SeoTitle),
            SeoDescription = Clean(req.SeoDescription),
            OpenBoxReason = Clean(req.OpenBoxReason),
            IsOpenBox = req.IsOpenBox,
            BatteryHealthPercent = req.BatteryHealthPercent,
            TechnicalState = req.TechnicalState,
            TechnicalNotes = Clean(req.TechnicalNotes),
            Active = req.Active,
            MostrarLojaOnline = req.MostrarLojaOnline,
            FornecedorId = req.FornecedorId,
        };
        if (req.Images is not null)
        {
            foreach (var (img, idx) in req.Images.Select((i, idx) => (i, idx)))
            {
                entity.Images.Add(new ProductImage
                {
                    TenantId = tenantId,
                    Url = img.Url.Trim(),
                    Alt = Clean(img.Alt),
                    Ordem = img.Ordem != 0 ? img.Ordem : idx,
                    IsCurated = img.IsCurated,
                });
            }
        }

        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Product), entity.Id, new { entity.Sku, entity.Brand, entity.Model, entity.PriceCents }, ct: ct);
        // Sprint 125: loja online só vê produtos com MostrarLojaOnline=true.
        if (entity.MostrarLojaOnline) await PublishCatalogEventAsync(WebhookEvents.PhonesAdicionado, entity, ct);
        // Sprint 130: produto novo já abaixo do threshold (raro mas possível — ex: backorder).
        if (entity.Active && IsStockBaixo(entity.StockQuantity, entity.StockMinima))
            await PublishCatalogEventAsync(WebhookEvents.PhonesStockBaixo, entity, ct);
        return ToDto(entity);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductWriteRequest req, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Product", id);
        Validate(req);
        var previousMostrar = entity.MostrarLojaOnline;
        var previousStockOk = !IsStockBaixo(entity.StockQuantity, entity.StockMinima);
        entity.Sku = await EnsureUniqueSkuAsync(req.Sku ?? entity.Sku, entity.Id, req.Brand, req.Model, ct);
        entity.Slug = await EnsureUniqueSlugAsync(req.Slug ?? entity.Slug, entity.Id, req.Brand, req.Model, req.Storage, req.Color, req.Grading, ct);
        entity.Brand = req.Brand.Trim();
        entity.Model = req.Model.Trim();
        entity.Storage = Clean(req.Storage);
        entity.Color = Clean(req.Color);
        entity.Origin = req.Origin;
        entity.Grade = req.Grade;
        // Sprint 197: Grading recalculado server-side.
        entity.Grading = ProductGradingMapper.ComposeLegacy(req.Origin, req.Grade);
        entity.SupplyType = req.SupplyType;
        entity.Category = req.Category;
        entity.DropshipSupplierSku = Clean(req.DropshipSupplierSku);
        entity.PriceCents = req.PriceCents;
        entity.CompareAtPriceCents = req.CompareAtPriceCents;
        entity.StockQuantity = req.StockQuantity;
        entity.StockMinima = req.StockMinima;
        entity.CustoUnitarioCents = req.CustoUnitarioCents;
        entity.DescriptionMarkdown = Clean(req.DescriptionMarkdown);
        entity.AttributesJson = Clean(req.AttributesJson);
        entity.SeoTitle = Clean(req.SeoTitle);
        entity.SeoDescription = Clean(req.SeoDescription);
        entity.OpenBoxReason = Clean(req.OpenBoxReason);
        entity.IsOpenBox = req.IsOpenBox;
        entity.BatteryHealthPercent = req.BatteryHealthPercent;
        entity.TechnicalState = req.TechnicalState;
        entity.TechnicalNotes = Clean(req.TechnicalNotes);
        entity.Active = req.Active;
        entity.MostrarLojaOnline = req.MostrarLojaOnline;
        entity.FornecedorId = req.FornecedorId;

        if (req.Images is not null)
        {
            // Sprint 156c: diff por URL em vez de Clear()+Add(). O padrão antigo gerava
            // DbUpdateConcurrencyException — o Clear() marca entities como Deleted →
            // StampAuditFields converte para Modified + IsDeleted=true, mas EF perdia track
            // dos Added (provavelmente conflito de identity dentro do mesmo SaveChanges).
            // Diff in-place é mais robusto e gera menos commands SQL.
            var tenantId = _tenant.TenantId ?? entity.TenantId;
            var existing = entity.Images.ToList();
            var incomingUrls = req.Images.Select(i => i.Url.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Remover imagens que já não estão na lista nova.
            foreach (var img in existing.Where(e => !incomingUrls.Contains(e.Url)).ToList())
                entity.Images.Remove(img);

            // 2. Update in-place ou Add novo por URL.
            var existingByUrl = entity.Images.ToDictionary(e => e.Url, e => e, StringComparer.OrdinalIgnoreCase);
            foreach (var (img, idx) in req.Images.Select((i, idx) => (i, idx)))
            {
                var url = img.Url.Trim();
                if (existingByUrl.TryGetValue(url, out var match))
                {
                    match.Alt = Clean(img.Alt);
                    match.Ordem = img.Ordem != 0 ? img.Ordem : idx;
                    match.IsCurated = img.IsCurated;
                }
                else
                {
                    entity.Images.Add(new ProductImage
                    {
                        TenantId = tenantId,
                        Url = url,
                        Alt = Clean(img.Alt),
                        Ordem = img.Ordem != 0 ? img.Ordem : idx,
                        IsCurated = img.IsCurated,
                    });
                }
            }
        }

        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Product), entity.Id, new { entity.Sku, entity.PriceCents, entity.StockQuantity, entity.Active }, ct: ct);

        // Sprint 125: 3 transições da flag MostrarLojaOnline.
        if (!previousMostrar && entity.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PhonesAdicionado, entity, ct);
        else if (previousMostrar && !entity.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PhonesRemovido, entity, ct);
        else if (entity.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PhonesAtualizado, entity, ct);

        // Sprint 130: stock baixo só na transição above→below.
        if (entity.Active && previousStockOk && IsStockBaixo(entity.StockQuantity, entity.StockMinima))
            await PublishCatalogEventAsync(WebhookEvents.PhonesStockBaixo, entity, ct);

        return ToDto(entity);
    }

    /// <summary>Sprint 130: produto está abaixo do mínimo. StockMinima=0 desliga o alerta.</summary>
    private static bool IsStockBaixo(int stockQuantity, int stockMinima)
        => stockMinima > 0 && stockQuantity <= stockMinima;

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Product", id);
        var wasInCatalog = entity.MostrarLojaOnline;
        _repo.Remove(entity);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(Product), id, new { entity.Sku }, ct: ct);
        if (wasInCatalog) await PublishCatalogEventAsync(WebhookEvents.PhonesRemovido, entity, ct);
    }

    public async Task RepublishWebhookAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await _repo.FindByIdAsync(productId, ct) ?? throw new NotFoundException("Product", productId);
        // Re-publica o estado actual como phones.atualizado (mesmo que não esteja shop online —
        // a loja decide se ignora). Útil para backfill após optimização de imagens.
        await PublishCatalogEventAsync(WebhookEvents.PhonesAtualizado, product, ct);
    }

    private async Task PublishCatalogEventAsync(string eventType, Product product, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return;

        // Sprint 154: payload alinhado com spec do outro Claude (ecommerce/Contexto/17).
        // Loja consome estes campos directamente para upsert local. Inclui derivados (isDropship,
        // shopConditionTier via mapper) e imagens curadas first com fallback raw.
        var curatedSource = product.Images.Where(i => i.IsCurated).OrderBy(i => i.Ordem).ToList();
        if (curatedSource.Count == 0) curatedSource = product.Images.OrderBy(i => i.Ordem).ToList();

        // Sprint 190: shopImagesCurated agora é mixed array. Imagens optimizadas (Sprint 189)
        // entram como ShopImage object com sizes/blur/dims; legacy (OptimizedAt=NULL) entram
        // como string simples. Spec em Contexto/62. Shop tolera ambos.
        var imageUrls = curatedSource.Select((i, idx) => i.OptimizedAt is not null
            ? (object)new
            {
                url = i.Url,
                url480w = i.Url480w,
                url1024w = i.Url1024w,
                url2048w = i.Url2048w,
                blurDataUrl = i.BlurDataUrl,
                width = i.Width,
                height = i.Height,
                alt = i.Alt,
                order = idx,
            }
            : i.Url).ToList();

        await _webhooks.PublishAsync(tenantId, eventType, new
        {
            productId = product.Id,
            sku = product.Sku,
            brand = product.Brand,
            model = product.Model,
            storage = product.Storage,
            color = product.Color,
            // Sprint 122 internal grading (deprecated, back-compat).
            grading = product.Grading.ToString(),
            // Sprint 146: canónicos para a loja (A+/A/B/C/OpenBox + label PT).
            gradingCanonical = ProductGradingMapper.ToCanonical(product.Grading),
            gradingLabel = ProductGradingMapper.ToLabelPt(product.Grading),
            // Sprint 197: 2D classification — loja deve usar isto em vez do grading legacy.
            origin = product.Origin.ToString().ToLowerInvariant(),
            originLabel = ProductGradingMapper.OriginLabelPt(product.Origin),
            grade = ProductGradingMapper.GradeCanonical(product.Grade),
            gradeSlug = ProductGradingMapper.GradeSlug(product.Grade),
            gradeLabel = ProductGradingMapper.GradeLabelPt(product.Grade),
            conditionCombined = ProductGradingMapper.ComposedLabelPt(product.Origin, product.Grade),
            // Sprint 151: categoria de produto.
            category = product.Category.ToString().ToLowerInvariant(),
            // Sprint 154: derivados do spec.
            isDropship = product.SupplyType == ProductSupplyType.Dropship,
            dropshipSupplierCode = product.Fornecedor?.Code,
            dropshipSupplierSku = product.DropshipSupplierSku,
            publishToShop = product.MostrarLojaOnline,
            shopSlug = product.Slug,
            shopSeoTitle = product.SeoTitle,
            shopSeoDescription = product.SeoDescription,
            shopConditionTier = ShopConditionTierFromGrading(product.Grading),
            shopIsOpenBox = product.Grading == ProductGrading.OpenBox,
            shopOpenBoxReason = product.OpenBoxReason,
            // Sprint 205: flag explícita confirmada com shop Claude.
            isOpenBox = product.IsOpenBox,
            // Sprint 204: trust signals pedidos pelo shop Claude.
            batteryHealthPercent = product.BatteryHealthPercent,
            technicalState = product.TechnicalState == Core.Enums.ProductTechnicalState.Unknown
                ? null
                : product.TechnicalState.ToString().ToLowerInvariant().Replace("neveropened", "never_opened").Replace("originalparts", "original_parts"),
            technicalNotes = product.TechnicalNotes,
            shopCompareAtPriceCents = product.CompareAtPriceCents,
            shopImagesCurated = imageUrls,
            shopMarketingDescription = product.DescriptionMarkdown,
            // Money + stock (top-level por conveniência).
            priceCents = product.PriceCents,
            stockQuantity = product.StockQuantity,
            // Sprint 196: hint para a loja não mostrar '999 disponível' (parece scam) em dropship.
            // Loja decide texto: 'em stock' para 'own' OR 'por encomenda · entrega 3-5d' para 'dropship'.
            stockDisplayMode = product.SupplyType == ProductSupplyType.Dropship ? "on-demand" : "exact",
            attributesJson = product.AttributesJson,
            updatedAt = product.UpdatedAt ?? product.CreatedAt,
        }, ct);
    }

    /// <summary>
    /// Sprint 154: mapa interno Grading → tier de venda PT que a loja usa (new/used/refurbished).
    /// Premium e Novo → "new"; OpenBox e Recondicionado-like (GradeA/B) → "refurbished";
    /// GradeC (mais desgastado) → "used".
    /// </summary>
    private static string ShopConditionTierFromGrading(ProductGrading g) => g switch
    {
        ProductGrading.Novo => "new",
        ProductGrading.Premium => "new",
        ProductGrading.GradeA => "refurbished",
        ProductGrading.GradeB => "refurbished",
        ProductGrading.OpenBox => "refurbished",
        ProductGrading.GradeC => "used",
        _ => "used",
    };

    private static void Validate(ProductWriteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Brand)) throw new ValidationException("brand_required", "Marca obrigatória.");
        if (string.IsNullOrWhiteSpace(req.Model)) throw new ValidationException("model_required", "Modelo obrigatório.");
        if (req.PriceCents < 0) throw new ValidationException("price_invalid", "Preço não pode ser negativo.");
        if (req.StockQuantity < 0) throw new ValidationException("stock_invalid", "Stock não pode ser negativo.");
        if (req.CustoUnitarioCents < 0) throw new ValidationException("custo_invalido", "Custo unitário não pode ser negativo.");
    }

    private async Task<string> EnsureUniqueSkuAsync(string? rawSku, Guid? excludeId, string brand, string model, CancellationToken ct)
    {
        var sku = string.IsNullOrWhiteSpace(rawSku)
            ? GenerateSku(brand, model)
            : rawSku.Trim().ToUpperInvariant();
        if (await _repo.SkuExistsAsync(sku, excludeId, ct))
            throw new ConflictException("sku_in_use", $"SKU '{sku}' já existe.");
        return sku;
    }

    private async Task<string> EnsureUniqueSlugAsync(string? rawSlug, Guid? excludeId, string brand, string model, string? storage, string? color, ProductGrading grading, CancellationToken ct)
    {
        var baseSlug = string.IsNullOrWhiteSpace(rawSlug)
            ? Slugify($"{brand} {model} {storage} {color} {grading}")
            : Slugify(rawSlug);

        // Sprint 236 fix: para imports CSV onde 2 linhas geram mesmo slug (ex: mesmo modelo
        // e storage e cor mas variantes diferentes do fornecedor), gerar slug-2, slug-3 em
        // vez de ConflictException. Para CreateAsync/UpdateAsync (rawSlug explícito), mantém
        // comportamento estrito: throw se Bruno escreveu slug que colide.
        if (!string.IsNullOrWhiteSpace(rawSlug))
        {
            if (await _repo.SlugExistsAsync(baseSlug, excludeId, ct))
                throw new ConflictException("slug_in_use", $"Slug '{baseSlug}' já existe.");
            return baseSlug;
        }

        // Slug auto-gerado: itera até encontrar único.
        var candidate = baseSlug;
        for (var n = 2; n < 1000; n++)
        {
            if (!await _repo.SlugExistsAsync(candidate, excludeId, ct))
                return candidate;
            candidate = $"{baseSlug}-{n}";
        }
        throw new ConflictException("slug_in_use", $"Slug '{baseSlug}' tem >1000 variantes.");
    }

    private static string GenerateSku(string brand, string model)
        => (brand[..Math.Min(3, brand.Length)] + "-" + model.Replace(" ", "") + "-" + Guid.NewGuid().ToString("N")[..6]).ToUpperInvariant();

    private static string Slugify(string input)
    {
        var sb = new StringBuilder();
        var prev = '-';
        foreach (var ch in input.ToLowerInvariant().Trim())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); prev = ch; }
            else if (prev != '-') { sb.Append('-'); prev = '-'; }
        }
        return sb.ToString().Trim('-');
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Sprint 153: importer CSV Molano (e similares dropship). Upsert idempotente por
    /// (FornecedorId, DropshipSupplierSku). Default: SupplyType=Dropship, MostrarLojaOnline=false
    /// (Bruno escolhe o que publicar). Imagens marcadas IsCurated=false (raw do supplier).
    ///
    /// Header CSV aceite (case-insensitive):
    /// sku, brand, model, storage, color, grading, price, stock, images (comma-separated URLs)
    /// </summary>
    /// <summary>
    /// Sprint 203: import CSV genérico usando mapping explícito (Claude-detected ou Bruno-defined).
    /// Substitui ImportMolanoCsvAsync para fornecedores novos. Se saveMapping=true, persiste
    /// no Fornecedor.CsvColumnMappingJson para próximos uploads automáticos.
    /// </summary>
    public async Task<ImportProductsResponse> ImportCsvWithMappingAsync(
        string csv,
        Guid fornecedorId,
        CsvImportMapping mapping,
        bool saveMapping,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv)) throw new ValidationException("csv_vazio", "CSV vazio.");
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var rows = RepairDesk.Common.Helpers.CsvParser.Parse(csv);
        if (rows.Count < 2) throw new ValidationException("csv_sem_dados", "CSV precisa header + 1 linha.");

        var header = rows[0].Select(h => h.Trim()).ToArray();
        int Idx(string? col) => string.IsNullOrEmpty(col) ? -1
            : Array.FindIndex(header, h => h.Equals(col, StringComparison.OrdinalIgnoreCase));

        var iSku = Idx(mapping.Sku);
        var iBrand = Idx(mapping.Brand);
        var iModel = Idx(mapping.Model);
        var iProduct = Idx(mapping.Product);
        var iStorage = Idx(mapping.Storage);
        var iColor = Idx(mapping.Color);
        var iGrading = Idx(mapping.Grading);
        var iPrice = Idx(mapping.Price);
        var iStock = Idx(mapping.Stock);
        var iCusto = Idx(mapping.Cost);
        var iImages = Idx(mapping.Images);

        if (iSku < 0 || iPrice < 0)
            throw new ValidationException("mapping_invalido", "Mapping precisa SKU + Price.");
        if (iBrand < 0 && iModel < 0 && iProduct < 0)
            throw new ValidationException("mapping_invalido", "Mapping precisa Brand+Model OU Product.");

        // Persist mapping se Bruno confirmou via saveMapping=true.
        if (saveMapping)
        {
            var fornecedor = await _fornecedores.FindByIdAsync(fornecedorId, ct);
            if (fornecedor is not null)
            {
                fornecedor.CsvColumnMappingJson = System.Text.Json.JsonSerializer.Serialize(mapping);
                await _fornecedores.SaveAsync(ct);
            }
        }

        return await ImportRowsAsync(rows, fornecedorId, iSku, iBrand, iModel, iProduct,
            iStorage, iColor, iGrading, iPrice, iStock, iCusto, iImages, ct);
    }

    public async Task<ImportProductsResponse> ImportMolanoCsvAsync(string csv, Guid fornecedorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new ValidationException("csv_vazio", "CSV vazio.");
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var rows = RepairDesk.Common.Helpers.CsvParser.Parse(csv);
        if (rows.Count < 2)
            throw new ValidationException("csv_sem_dados", "CSV precisa de header + pelo menos 1 linha.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        // Sprint 201: matching com prefix (para "Price (EUR)", "warranty 12m price", etc).
        // Exact match primeiro; se não, tenta prefix.
        int Idx(params string[] names) => header
            .Select((h, i) => new { h, i })
            .FirstOrDefault(x => names.Contains(x.h))?.i
            ?? header
                .Select((h, i) => new { h, i })
                .FirstOrDefault(x => names.Any(n => x.h.StartsWith(n + " ") || x.h.StartsWith(n + "(") || x.h == n))?.i
            ?? -1;

        var iSku = Idx("sku", "supplier_sku", "ref", "referencia");
        var iBrand = Idx("brand", "marca");
        var iModel = Idx("model", "modelo");
        // Sprint 201: Molano novo CSV (quick-order-export) usa "Product" como descrição combinada
        // em vez de Brand+Model separados. Se ambos faltam, usamos esta coluna como fallback.
        var iProduct = Idx("product", "produto", "description", "descricao");
        var iStorage = Idx("storage", "capacidade", "armazenamento");
        var iColor = Idx("color", "colour", "cor"); // UK spelling 'colour'
        var iGrading = Idx("grading", "grade", "condicao", "condição");
        var iPrice = Idx("price", "preco", "preço", "preco_venda");
        var iStock = Idx("stock", "qtd", "quantidade", "qtdstock");
        var iImages = Idx("images", "imagens", "image_urls");
        var iCusto = Idx("cost", "custo", "preco_compra");

        if (iSku < 0 || iPrice < 0)
            throw new ValidationException("csv_falta_coluna",
                "Colunas obrigatórias: SKU + Price (ou Brand + Model + Price). Detectado header: " + string.Join(", ", header));
        if ((iBrand < 0 || iModel < 0) && iProduct < 0)
            throw new ValidationException("csv_falta_coluna",
                "Precisa de Brand+Model separados OU coluna Product combinada. Header: " + string.Join(", ", header));

        return await ImportRowsAsync(rows, fornecedorId, iSku, iBrand, iModel, iProduct,
            iStorage, iColor, iGrading, iPrice, iStock, iCusto, iImages, ct);
    }

    /// <summary>Sprint 203: helper compartilhado entre ImportMolanoCsvAsync (Sprint 153/201)
    /// e ImportCsvWithMappingAsync (universal). Recebe os índices já calculados.</summary>
    private async Task<ImportProductsResponse> ImportRowsAsync(
        IReadOnlyList<string[]> rows,
        Guid fornecedorId,
        int iSku, int iBrand, int iModel, int iProduct,
        int iStorage, int iColor, int iGrading, int iPrice,
        int iStock, int iCusto, int iImages,
        CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var errors = new List<ImportProductError>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        // Sprint 236 fix: track slugs já adicionados no batch (sem SaveAsync), para 2 linhas
        // do mesmo CSV com mesmo slug gerarem slug-2, slug-3 em vez de duplicate key na DB.
        var slugsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var lineNo = i + 1;
            var row = rows[i];
            string? Get(int idx) => idx >= 0 && idx < row.Length ? row[idx]?.Trim() : null;

            var supplierSku = Get(iSku);
            if (string.IsNullOrWhiteSpace(supplierSku))
            {
                errors.Add(new ImportProductError(lineNo, "sku", "SKU em branco.", null));
                continue;
            }

            var brand = Get(iBrand);
            var model = Get(iModel);
            // Sprint 201: fallback Molano novo formato — extrair brand+model de "Product" combinado.
            // Ex: "iPad 10 (2022) 256GB 4G – 256 · Blue · Grade B" → Brand=Apple (inferido), Model=iPad 10 (2022)
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                var productText = Get(iProduct);
                if (!string.IsNullOrWhiteSpace(productText))
                {
                    // Heurística: primeiro segmento (antes do "–" ou "·") é o modelo
                    var firstSegment = productText.Split(new[] { '–', '·', '-' }, 2)[0].Trim();
                    if (string.IsNullOrWhiteSpace(brand))
                    {
                        // Inferir brand das palavras-chave conhecidas
                        var lower = firstSegment.ToLowerInvariant();
                        brand = lower.StartsWith("iphone") || lower.StartsWith("ipad") || lower.StartsWith("macbook") || lower.StartsWith("imac") || lower.StartsWith("apple") || lower.StartsWith("watch") ? "Apple"
                              : lower.StartsWith("galaxy") || lower.StartsWith("samsung") ? "Samsung"
                              : lower.StartsWith("pixel") || lower.StartsWith("google") ? "Google"
                              : lower.StartsWith("redmi") || lower.StartsWith("xiaomi") || lower.StartsWith("poco") ? "Xiaomi"
                              : lower.StartsWith("oppo") ? "OPPO"
                              : lower.StartsWith("oneplus") ? "OnePlus"
                              : lower.StartsWith("huawei") ? "Huawei"
                              : firstSegment.Split(' ', 2)[0]; // primeiro token
                    }
                    if (string.IsNullOrWhiteSpace(model)) model = firstSegment;
                }
            }
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                errors.Add(new ImportProductError(lineNo, "brand/model", "Marca ou modelo em branco (CSV não tem colunas separadas nem coluna Product utilizável).", supplierSku));
                continue;
            }

            var priceText = Get(iPrice);
            if (!TryParseCents(priceText, out var priceCents) || priceCents <= 0)
            {
                errors.Add(new ImportProductError(lineNo, "price", $"Preço inválido: '{priceText}'.", supplierSku));
                continue;
            }

            var stockText = Get(iStock);
            var stockQuantity = int.TryParse(stockText, out var sq) ? sq : 0;
            var custoText = Get(iCusto);
            TryParseCents(custoText, out var custoCents);
            var grading = ParseGrading(Get(iGrading));
            var imagesRaw = Get(iImages);

            try
            {
                var existing = await _repo.FindByDropshipAsync(fornecedorId, supplierSku, ct);
                if (existing is null)
                {
                    // Auto-gera SKU interno + slug.
                    var internalSku = await EnsureUniqueSkuAsync(null, null, brand, model, ct);
                    var slug = await EnsureUniqueSlugAsync(null, null, brand, model, Get(iStorage), Get(iColor), grading, ct);
                    // Sprint 236: garantir que outra linha do mesmo batch não gerou o mesmo slug.
                    if (!slugsInBatch.Add(slug))
                    {
                        var baseSlug = slug;
                        for (var n = 2; n < 1000; n++)
                        {
                            var candidate = $"{baseSlug}-{n}";
                            if (!await _repo.SlugExistsAsync(candidate, null, ct) && slugsInBatch.Add(candidate))
                            {
                                slug = candidate;
                                break;
                            }
                        }
                    }

                    var entity = new Product
                    {
                        TenantId = tenantId,
                        Sku = internalSku,
                        Slug = slug,
                        Brand = brand,
                        Model = model,
                        Storage = Clean(Get(iStorage)),
                        Color = Clean(Get(iColor)),
                        Grading = grading,
                        SupplyType = ProductSupplyType.Dropship,
                        Category = ProductCategory.Phone,
                        DropshipSupplierSku = supplierSku,
                        PriceCents = priceCents,
                        StockQuantity = stockQuantity,
                        CustoUnitarioCents = custoCents,
                        Active = true,
                        MostrarLojaOnline = false,
                        FornecedorId = fornecedorId,
                    };
                    foreach (var (url, idx) in ParseImageUrls(imagesRaw).Select((u, idx) => (u, idx)))
                    {
                        entity.Images.Add(new ProductImage
                        {
                            TenantId = tenantId,
                            Url = url,
                            Ordem = idx,
                            IsCurated = false,
                        });
                    }
                    await _repo.AddAsync(entity, ct);
                    created++;
                }
                else
                {
                    // Update: actualiza preço, stock, imagens (mantém MostrarLojaOnline + Slug + IsCurated manual).
                    existing.PriceCents = priceCents;
                    existing.StockQuantity = stockQuantity;
                    if (custoCents > 0) existing.CustoUnitarioCents = custoCents;
                    existing.Grading = grading;
                    // Se não há imagens curadas, substitui as raw existentes pelas novas raw.
                    if (!existing.Images.Any(img => img.IsCurated))
                    {
                        existing.Images.Clear();
                        foreach (var (url, idx) in ParseImageUrls(imagesRaw).Select((u, idx) => (u, idx)))
                        {
                            existing.Images.Add(new ProductImage
                            {
                                TenantId = tenantId,
                                Url = url,
                                Ordem = idx,
                                IsCurated = false,
                            });
                        }
                    }
                    updated++;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ImportProductError(lineNo, "general", ex.Message, supplierSku));
                skipped++;
            }
        }

        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Product), null, new
        {
            operation = "molano_csv_import",
            fornecedorId,
            created,
            updated,
            skipped,
            errors = errors.Count,
        }, ct: ct);

        return new ImportProductsResponse(created, updated, skipped, errors);
    }

    /// <summary>
    /// Sprint 155: migra produtos shop-only do dump JSON da loja online. Upsert por SKU.
    /// Mapeia category string → ProductCategory enum + grading "A+"/"A"/"B+"/"B"/"C" → ProductGrading.
    /// Todos ficam MostrarLojaOnline=true, Active=isActive do dump.
    /// </summary>
    public async Task<ImportProductsResponse> MigrateShopProductsAsync(IReadOnlyList<MigrateShopProductRequest> products, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        if (products is null || products.Count == 0)
            return new ImportProductsResponse(0, 0, 0, Array.Empty<ImportProductError>());

        var errors = new List<ImportProductError>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        for (var i = 0; i < products.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var p = products[i];
            var lineNo = i + 1;

            if (string.IsNullOrWhiteSpace(p.Sku) || string.IsNullOrWhiteSpace(p.Brand) || string.IsNullOrWhiteSpace(p.Model))
            {
                errors.Add(new ImportProductError(lineNo, "sku/brand/model", "Campos obrigatórios em falta.", p.Sku));
                continue;
            }

            try
            {
                var category = MapShopCategory(p.Category);
                var grading = MapShopGrading(p.Grading, p.IsOpenBox);
                var existing = await _repo.SkuExistsAsync(p.Sku.Trim().ToUpperInvariant(), null, ct);

                if (existing)
                {
                    // SKU já existe — ignora (Bruno pode escolher manualmente actualizar via UI).
                    skipped++;
                    continue;
                }

                // Auto-gera slug se conflito.
                var slug = await EnsureUniqueSlugAsync(null, null, p.Brand, p.Model, p.Storage, p.Color, grading, ct);

                var entity = new Product
                {
                    TenantId = tenantId,
                    Sku = p.Sku.Trim().ToUpperInvariant(),
                    Slug = slug,
                    Brand = p.Brand.Trim(),
                    Model = p.Model.Trim(),
                    Storage = Clean(p.Storage),
                    Color = Clean(p.Color),
                    Grading = grading,
                    SupplyType = ProductSupplyType.Stock,
                    Category = category,
                    PriceCents = p.PriceCents,
                    CompareAtPriceCents = p.CompareAtPriceCents,
                    StockQuantity = p.StockQuantity,
                    CustoUnitarioCents = 0,
                    DescriptionMarkdown = Clean(p.Description),
                    SeoTitle = Clean(p.SeoTitle),
                    SeoDescription = Clean(p.SeoDescription),
                    OpenBoxReason = Clean(p.OpenBoxReason),
                    Active = p.IsActive,
                    MostrarLojaOnline = true,
                };
                if (p.Images is { Count: > 0 })
                {
                    foreach (var (url, idx) in p.Images.Where(u => !string.IsNullOrWhiteSpace(u)).Select((u, idx) => (u, idx)))
                    {
                        entity.Images.Add(new ProductImage
                        {
                            TenantId = tenantId,
                            Url = url.Trim(),
                            Ordem = idx,
                            // Imagens vêm da loja (já curadas — não são raw de supplier CSV).
                            IsCurated = true,
                        });
                    }
                }

                await _repo.AddAsync(entity, ct);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportProductError(lineNo, "general", ex.Message, p.Sku));
                skipped++;
            }
        }

        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Product), null, new
        {
            operation = "migrate_shop_only",
            total = products.Count,
            created,
            updated,
            skipped,
            errors = errors.Count,
        }, ct: ct);

        return new ImportProductsResponse(created, updated, skipped, errors);
    }

    private static ProductCategory MapShopCategory(string category) => category?.ToLowerInvariant() switch
    {
        "phone" => ProductCategory.Phone,
        "accessory_case" or "accessory_charger" or "accessory_screen_protector" or "accessory_cable"
            or "accessory" => ProductCategory.Accessory,
        _ => ProductCategory.Other,
    };

    private static ProductGrading MapShopGrading(string? grading, bool isOpenBox)
    {
        if (isOpenBox) return ProductGrading.OpenBox;
        return grading?.Trim().ToUpperInvariant() switch
        {
            "A+" or "PREMIUM" => ProductGrading.Premium,
            "A" => ProductGrading.GradeA,
            "B+" or "B" => ProductGrading.GradeB,
            "C+" or "C" => ProductGrading.GradeC,
            "NOVO" or "NEW" => ProductGrading.Novo,
            _ => ProductGrading.GradeA,
        };
    }

    private static bool TryParseCents(string? text, out int cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(",", ".");
        if (!decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v)) return false;
        cents = (int)Math.Round(v * 100m);
        return true;
    }

    private static ProductGrading ParseGrading(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ProductGrading.Novo;
        return text.Trim().ToLowerInvariant() switch
        {
            "novo" or "new" => ProductGrading.Novo,
            "grade a" or "gradea" or "a" => ProductGrading.GradeA,
            "grade b" or "gradeb" or "b" => ProductGrading.GradeB,
            "grade c" or "gradec" or "c" => ProductGrading.GradeC,
            "openbox" or "open box" or "open-box" => ProductGrading.OpenBox,
            "premium" or "a+" => ProductGrading.Premium,
            _ => ProductGrading.Novo,
        };
    }

    private static IEnumerable<string> ParseImageUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Take(10);
    }

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.Sku, p.Slug, p.Brand, p.Model, p.Storage, p.Color, p.Grading, p.Origin, p.Grade, p.SupplyType,
            p.Category, p.DropshipSupplierSku,
            p.PriceCents, p.CompareAtPriceCents,
            p.StockQuantity, p.StockMinima, p.CustoUnitarioCents,
            p.DescriptionMarkdown, p.AttributesJson, p.SeoTitle, p.SeoDescription,
            p.OpenBoxReason, p.IsOpenBox,
            p.BatteryHealthPercent, p.TechnicalState, p.TechnicalNotes,
            p.Active, p.MostrarLojaOnline, p.FornecedorId, p.Fornecedor?.Name, p.Fornecedor?.Code,
            p.Images.OrderBy(i => i.Ordem).Select(i => new ProductImageDto(i.Id, i.Url, i.Alt, i.Ordem, i.IsCurated)).ToList(),
            p.CreatedAt, p.UpdatedAt);
}
