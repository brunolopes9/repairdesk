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
}

public sealed record ImportProductsResponse(
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<ImportProductError> Errors);

public sealed record ImportProductError(int Line, string Field, string Message, string? Sku);

public sealed record ProductImageDto(Guid Id, string Url, string? Alt, int Ordem, bool IsCurated);

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Slug,
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    ProductGrading Grading,
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
    ProductGrading Grading,
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

    public ProductService(IProductRepository repo, ITenantContext tenant, IAuditLogger audit, IWebhookPublisher webhooks)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
        _webhooks = webhooks;
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
            Grading = req.Grading,
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
        entity.Grading = req.Grading;
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
        entity.Active = req.Active;
        entity.MostrarLojaOnline = req.MostrarLojaOnline;
        entity.FornecedorId = req.FornecedorId;

        if (req.Images is not null)
        {
            entity.Images.Clear();
            var tenantId = _tenant.TenantId ?? entity.TenantId;
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

    private async Task PublishCatalogEventAsync(string eventType, Product product, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return;
        await _webhooks.PublishAsync(tenantId, eventType, new
        {
            productId = product.Id,
            sku = product.Sku,
            slug = product.Slug,
            brand = product.Brand,
            model = product.Model,
            storage = product.Storage,
            color = product.Color,
            grading = product.Grading.ToString(),
            // Sprint 146: campos canónicos para alinhar com a loja headless.
            gradingCanonical = ProductGradingMapper.ToCanonical(product.Grading),
            gradingLabel = ProductGradingMapper.ToLabelPt(product.Grading),
            supplyType = product.SupplyType.ToString(),
            priceCents = product.PriceCents,
            stockQuantity = product.StockQuantity,
            mostrarLojaOnline = product.MostrarLojaOnline,
        }, ct);
    }

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
        var slug = string.IsNullOrWhiteSpace(rawSlug)
            ? Slugify($"{brand} {model} {storage} {color} {grading}")
            : Slugify(rawSlug);
        if (await _repo.SlugExistsAsync(slug, excludeId, ct))
            throw new ConflictException("slug_in_use", $"Slug '{slug}' já existe.");
        return slug;
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
        int Idx(params string[] names) => header
            .Select((h, i) => new { h, i })
            .FirstOrDefault(x => names.Contains(x.h))?.i ?? -1;

        var iSku = Idx("sku", "supplier_sku", "ref", "referencia");
        var iBrand = Idx("brand", "marca");
        var iModel = Idx("model", "modelo");
        var iStorage = Idx("storage", "capacidade", "armazenamento");
        var iColor = Idx("color", "cor");
        var iGrading = Idx("grading", "grade", "condicao", "condição");
        var iPrice = Idx("price", "preco", "preço", "preco_venda");
        var iStock = Idx("stock", "qtd", "quantidade", "qtdstock");
        var iImages = Idx("images", "imagens", "image_urls");
        var iCusto = Idx("cost", "custo", "preco_compra");

        if (iSku < 0 || iBrand < 0 || iModel < 0 || iPrice < 0)
            throw new ValidationException("csv_falta_coluna",
                "Colunas obrigatórias: sku, brand, model, price. Aceito também storage, color, grading, stock, images, cost.");

        var errors = new List<ImportProductError>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

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
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                errors.Add(new ImportProductError(lineNo, "brand/model", "Marca ou modelo em branco.", supplierSku));
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
        new(p.Id, p.Sku, p.Slug, p.Brand, p.Model, p.Storage, p.Color, p.Grading, p.SupplyType,
            p.Category, p.DropshipSupplierSku,
            p.PriceCents, p.CompareAtPriceCents,
            p.StockQuantity, p.StockMinima, p.CustoUnitarioCents,
            p.DescriptionMarkdown, p.AttributesJson, p.SeoTitle, p.SeoDescription,
            p.OpenBoxReason,
            p.Active, p.MostrarLojaOnline, p.FornecedorId, p.Fornecedor?.Name, p.Fornecedor?.Code,
            p.Images.OrderBy(i => i.Ordem).Select(i => new ProductImageDto(i.Id, i.Url, i.Alt, i.Ordem, i.IsCurated)).ToList(),
            p.CreatedAt, p.UpdatedAt);
}
