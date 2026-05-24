using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Products;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    private readonly AppDbContext _db;
    private readonly IProductSeoGenerator _seoGen;
    private readonly IImageOptimizationService _imageOpt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService service,
        AppDbContext db,
        IProductSeoGenerator seoGen,
        IImageOptimizationService imageOpt,
        IHttpClientFactory httpFactory,
        ILogger<ProductsController> logger)
    {
        _service = service;
        _db = db;
        _seoGen = seoGen;
        _imageOpt = imageOpt;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    [HttpGet]
    public Task<PagedResult<ProductDto>> Search(
        [FromQuery] string? search,
        [FromQuery] string? brand,
        [FromQuery] bool? lojaOnline,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _service.SearchAsync(search, brand, lojaOnline, includeInactive, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public Task<ProductDto> Create([FromBody] ProductWriteRequest req, CancellationToken ct) => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, [FromBody] ProductWriteRequest req, CancellationToken ct) => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sprint 153: importer CSV Molano (e similares dropship). Body: { fornecedorId, csv }.
    /// Upsert idempotente — re-importar mesmo CSV não duplica produtos.
    /// </summary>
    [HttpPost("import-molano")]
    [Authorize(Roles = "Admin")]
    public Task<ImportProductsResponse> ImportMolano([FromBody] ImportMolanoRequest req, CancellationToken ct)
        => _service.ImportMolanoCsvAsync(req.Csv, req.FornecedorId, ct);

    /// <summary>
    /// Sprint 203: detecta mapeamento de colunas CSV via Claude. Bruno envia primeiras linhas
    /// dum CSV novo (sem precisar de Molano-specific code) e Claude sugere o mapping. Bruno
    /// confirma 1× no UI, mapping fica guardado no Fornecedor, próximos imports são automáticos.
    /// Custo ~0.05¢ por análise.
    /// </summary>
    [HttpPost("csv/detect-columns")]
    public async Task<IActionResult> DetectCsvColumns(
        [FromBody] DetectCsvColumnsRequest req,
        [FromServices] RepairDesk.Services.Products.ICsvColumnDetector detector,
        [FromServices] RepairDesk.Core.Abstractions.IFornecedorRepository fornecedores,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Csv))
            return BadRequest(new { code = "csv_empty" });

        var rows = RepairDesk.Common.Helpers.CsvParser.Parse(req.Csv);
        if (rows.Count < 2)
            return BadRequest(new { code = "csv_too_short", detail = "CSV precisa header + pelo menos 1 linha." });

        var header = rows[0].Select(h => h.Trim()).ToArray();
        var samples = rows.Skip(1).Take(3).ToList();

        // Sprint 209: auto-skip Claude se Fornecedor já tem mapping guardado.
        // Poupa ~0.05¢ + 2-3s por upload de fornecedor recorrente (Molano, Tudo4Mobile, etc).
        if (req.FornecedorId is Guid fId && fId != Guid.Empty)
        {
            var fornecedor = await fornecedores.FindByIdAsync(fId, ct);
            if (fornecedor?.CsvColumnMappingJson is { Length: > 0 } cached)
            {
                try
                {
                    var savedMapping = System.Text.Json.JsonSerializer.Deserialize<RepairDesk.Services.Products.CsvImportMapping>(cached);
                    if (savedMapping is not null)
                    {
                        return Ok(new
                        {
                            detected = true,
                            mapping = new
                            {
                                sku = savedMapping.Sku,
                                brand = savedMapping.Brand,
                                model = savedMapping.Model,
                                product = savedMapping.Product,
                                storage = savedMapping.Storage,
                                color = savedMapping.Color,
                                grading = savedMapping.Grading,
                                price = savedMapping.Price,
                                stock = savedMapping.Stock,
                                cost = savedMapping.Cost,
                                images = savedMapping.Images,
                                confidence = "cached",
                                notes = $"Mapping guardado para {fornecedor.Name}. Não chamei Claude.",
                            },
                            header,
                            samplesShown = samples.Count,
                            source = "cache",
                        });
                    }
                }
                catch { /* JSON inválido → fallback Claude */ }
            }
        }

        var mapping = await detector.DetectAsync(header, samples, ct);
        if (mapping is null)
            return Ok(new { detected = false, reason = "LLM indisponível ou quota esgotada — completa o mapping manualmente.", header, source = "llm-unavailable" });

        return Ok(new { detected = true, mapping, header, samplesShown = samples.Count, source = "claude" });
    }

    /// <summary>
    /// Sprint 203: importar CSV usando mapping (do Fornecedor.CsvColumnMappingJson aprendido,
    /// ou enviado no request após Bruno confirmar). Se saveMapping=true e fornecedorId existe,
    /// guarda mapping no Fornecedor para próximos uploads automáticos.
    /// </summary>
    [HttpPost("csv/import-with-mapping")]
    [Authorize(Roles = "Admin")]
    public Task<ImportProductsResponse> ImportCsvWithMapping(
        [FromBody] ImportCsvWithMappingRequest req,
        CancellationToken ct)
        => _service.ImportCsvWithMappingAsync(req.Csv, req.FornecedorId, req.Mapping, req.SaveMapping, ct);

    /// <summary>
    /// Sprint 155: migração one-off de produtos shop-only (existiam só na loja antes do
    /// single-source-of-truth). Outro Claude gera o JSON via npm run db:export-shop-only.
    /// Upsert por SKU — re-correr é seguro (skip existentes).
    /// </summary>
    [HttpPost("migrate-shop")]
    [Authorize(Roles = "Admin")]
    public Task<ImportProductsResponse> MigrateShop([FromBody] MigrateShopRequest req, CancellationToken ct)
        => _service.MigrateShopProductsAsync(req.Products, ct);

    /// <summary>
    /// Sprint 166a: gera pacote SEO completo (title, description, alt, markdown) via Claude
    /// para um produto. Insight: um iPhone 17 é sempre um iPhone 17 — uma chamada por produto
    /// chega para gerar tudo o conteúdo SEO; alt aplica-se a todas as fotos.
    /// Custo ~0.5¢/produto.
    /// </summary>
    [HttpPost("{productId:guid}/generate-seo")]
    public async Task<IActionResult> GenerateSeo(Guid productId, [FromQuery] bool includeImage = true, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Images.OrderBy(i => i.Ordem))
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return NotFound(new { code = "product_not_found" });

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (includeImage)
        {
            var firstImage = product.Images.FirstOrDefault();
            if (firstImage is not null)
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    http.Timeout = TimeSpan.FromSeconds(20);
                    using var resp = await http.GetAsync(firstImage.Url, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        imageBytes = await resp.Content.ReadAsByteArrayAsync(ct);
                        imageMime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Generate-seo: falhou fetch imagem {Url} — continua só com texto", firstImage.Url);
                }
            }
        }

        var input = new ProductSeoInput(
            Brand: product.Brand,
            Model: product.Model,
            Storage: product.Storage,
            Color: product.Color,
            Condition: RepairDesk.Services.Products.ProductGradingMapper.ToLabelPt(product.Grading),
            ExtraContext: product.SeoTitle);

        var seo = await _seoGen.GenerateAsync(input, imageBytes, imageMime, ct);
        if (seo is null)
            return BadRequest(new { code = "llm_unavailable", detail = "Anthropic API indisponível ou quota esgotada." });

        // Aplicar SEO meta ao produto (alts ficam para o endpoint per-imagem).
        product.SeoTitle = seo.SeoTitle;
        product.SeoDescription = seo.SeoDescription;
        if (string.IsNullOrWhiteSpace(product.DescriptionMarkdown))
            product.DescriptionMarkdown = seo.DescriptionMarkdown;
        // Sprint 199: popula AttributesJson se ainda vazio — Bruno reclamou que este campo ficava
        // sempre vazio. Claude devolve dict structured (display/chip/connector/sim/etc) só quando
        // tem certeza absoluta para o modelo.
        if (string.IsNullOrWhiteSpace(product.AttributesJson) && !string.IsNullOrWhiteSpace(seo.AttributesJson))
            product.AttributesJson = seo.AttributesJson;
        await _db.SaveChangesAsync(ct);

        return Ok(new GenerateSeoResponse(
            SeoTitle: seo.SeoTitle,
            SeoDescription: seo.SeoDescription,
            Alt: seo.Alt,
            DescriptionMarkdown: seo.DescriptionMarkdown,
            AttributesJson: seo.AttributesJson,
            ImagesUpdated: 0));  // não tocamos em alts existentes — usa generate-alt per imagem
    }

    /// <summary>
    /// Sprint 166a: gera alt text específico para UMA imagem (Vision). Custo ~0.5¢.
    /// Alts diferentes por foto melhoram SEO Google Images.
    /// </summary>
    [HttpPost("{productId:guid}/images/{imageId:guid}/generate-alt")]
    public async Task<IActionResult> GenerateImageAlt(Guid productId, Guid imageId, CancellationToken ct)
    {
        var image = await _db.ProductImages
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
        if (image is null || image.Product is null) return NotFound(new { code = "image_not_found" });

        byte[] bytes;
        string mimeType;
        try
        {
            using var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            using var resp = await http.GetAsync(image.Url, ct);
            if (!resp.IsSuccessStatusCode)
                return BadRequest(new { code = "image_unreachable", detail = $"GET {image.Url} -> {(int)resp.StatusCode}" });
            bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            mimeType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GenerateImageAlt: falhou fetch {Url}", image.Url);
            return BadRequest(new { code = "image_fetch_failed", detail = ex.Message });
        }

        var ctx = new ProductSeoInput(
            Brand: image.Product.Brand,
            Model: image.Product.Model,
            Storage: image.Product.Storage,
            Color: image.Product.Color,
            Condition: RepairDesk.Services.Products.ProductGradingMapper.ToLabelPt(image.Product.Grading),
            ExtraContext: null);

        var alt = await _seoGen.GenerateAltAsync(bytes, mimeType, ctx, ct);
        if (alt is null) return BadRequest(new { code = "llm_unavailable" });

        image.Alt = alt;
        await _db.SaveChangesAsync(ct);
        return Ok(new { alt });
    }

    /// <summary>
    /// Sprint 190: força re-emit do webhook phones.atualizado para um produto. Útil após
    /// optimização de imagens (Sprint 189) ou para backfill. Spec doc 62.
    /// </summary>
    [HttpPost("{productId:guid}/republish-webhook")]
    public async Task<IActionResult> RepublishWebhook(Guid productId, CancellationToken ct)
    {
        await _service.RepublishWebhookAsync(productId, ct);
        return Ok(new { republished = true });
    }

    /// <summary>
    /// Sprint 195: upload imagem ANTES de produto existir. Bruno precisa de poder
    /// fazer upload no modal de criação. Devolve URLs optimizadas; frontend guarda
    /// no form e ao Create envia URLs no payload. Sem productId = sem ProductImage
    /// criado aqui — só optimiza e armazena no storage.
    /// </summary>
    [HttpPost("images/upload-pending")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadPendingImage(IFormFile image, CancellationToken ct)
    {
        if (image is null || image.Length == 0) return BadRequest(new { code = "no_image" });
        var mime = string.IsNullOrWhiteSpace(image.ContentType) ? "image/jpeg" : image.ContentType.ToLowerInvariant();
        if (mime is not "image/jpeg" and not "image/png" and not "image/webp" and not "image/gif")
            return BadRequest(new { code = "unsupported_mime" });

        var tenantId = User.FindFirst("tenant_id")?.Value ?? "shared";
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var keyPrefix = $"products/{tenantId}/pending/{Guid.NewGuid():N}";
        var optimized = await _imageOpt.OptimizeAsync(bytes, mime, keyPrefix, ct);
        return Ok(new
        {
            url = optimized.OriginalUrl,
            url480w = optimized.Url480w,
            url1024w = optimized.Url1024w,
            url2048w = optimized.Url2048w,
            blurDataUrl = optimized.BlurDataUrl,
            width = optimized.Width,
            height = optimized.Height,
        });
    }

    /// <summary>
    /// Sprint 195: gera SEO ANTES de produto existir. Recebe campos no body + opcional
    /// imageUrl (que vai buscar via HTTP) ou imagem inline. Devolve SEO sem persistir.
    /// </summary>
    [HttpPost("preview-seo")]
    public async Task<IActionResult> PreviewSeo([FromBody] PreviewSeoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Brand) || string.IsNullOrWhiteSpace(req.Model))
            return BadRequest(new { code = "brand_model_required" });

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (!string.IsNullOrWhiteSpace(req.ImageUrl))
        {
            try
            {
                using var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                using var resp = await http.GetAsync(req.ImageUrl, ct);
                if (resp.IsSuccessStatusCode)
                {
                    imageBytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    imageMime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "preview-seo: falhou fetch imagem {Url}", req.ImageUrl); }
        }

        var input = new ProductSeoInput(req.Brand, req.Model, req.Storage, req.Color, req.Condition, null);
        var seo = await _seoGen.GenerateAsync(input, imageBytes, imageMime, ct);
        if (seo is null) return BadRequest(new { code = "llm_unavailable" });
        return Ok(new { seo.SeoTitle, seo.SeoDescription, Alt = seo.Alt, seo.DescriptionMarkdown, seo.AttributesJson });
    }

    /// <summary>
    /// Sprint 189: upload imagem com pipeline SEO automático (Contexto/60).
    /// Recebe ficheiro arbitrário (PNG/JPG até 10MB), produz 3 WebP (480/1024/2048) +
    /// blur LQIP + dimensões, faz upload R2 e cria ProductImage com todas as URLs.
    /// </summary>
    [HttpPost("{productId:guid}/images/upload")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(Guid productId, IFormFile image, CancellationToken ct)
    {
        if (image is null || image.Length == 0) return BadRequest(new { code = "no_image" });
        var mime = string.IsNullOrWhiteSpace(image.ContentType) ? "image/jpeg" : image.ContentType.ToLowerInvariant();
        if (mime is not "image/jpeg" and not "image/png" and not "image/webp" and not "image/gif")
            return BadRequest(new { code = "unsupported_mime" });

        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return NotFound();

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var keyPrefix = $"products/{product.TenantId}/{productId}/{Guid.NewGuid():N}";
        var optimized = await _imageOpt.OptimizeAsync(bytes, mime, keyPrefix, ct);

        var newImage = new RepairDesk.Core.Entities.ProductImage
        {
            TenantId = product.TenantId,
            ProductId = productId,
            Url = optimized.OriginalUrl,
            Ordem = (product.Images.Count == 0 ? 0 : product.Images.Max(i => i.Ordem) + 1),
            IsCurated = true,
            Url480w = optimized.Url480w,
            Url1024w = optimized.Url1024w,
            Url2048w = optimized.Url2048w,
            BlurDataUrl = optimized.BlurDataUrl,
            Width = optimized.Width,
            Height = optimized.Height,
            OptimizedAt = DateTime.UtcNow,
        };
        product.Images.Add(newImage);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            imageId = newImage.Id,
            url = newImage.Url,
            url480w = newImage.Url480w,
            url1024w = newImage.Url1024w,
            url2048w = newImage.Url2048w,
            blurDataUrl = newImage.BlurDataUrl,
            width = newImage.Width,
            height = newImage.Height,
        });
    }

    /// <summary>
    /// Sprint 192: processa em batch imagens legacy (OptimizedAt=NULL) — descarrega URL
    /// original via HTTP, passa pelo pipeline Sprint 189, actualiza campos. Após cada
    /// produto, dispara webhook PhonesAtualizado (Sprint 190) para a loja receber sizes.
    ///
    /// Idempotente: imagens já optimizadas são ignoradas. Limita a N imagens por chamada
    /// (default 20) para não causar timeout — Bruno corre várias vezes até esgotar.
    /// </summary>
    [HttpPost("optimize-legacy-images")]
    public async Task<IActionResult> OptimizeLegacyImages([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100) return BadRequest(new { code = "invalid_limit", detail = "limit ∈ [1,100]" });

        var legacy = await _db.ProductImages
            .Where(i => i.OptimizedAt == null && i.Url.StartsWith("http"))
            .OrderBy(i => i.CreatedAt)
            .Take(limit)
            .Include(i => i.Product)
            .ToListAsync(ct);

        if (legacy.Count == 0) return Ok(new { processed = 0, remaining = 0, errors = Array.Empty<object>() });

        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        var errors = new List<object>();
        var touchedProducts = new HashSet<Guid>();
        var processed = 0;

        foreach (var img in legacy)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await http.GetAsync(img.Url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    errors.Add(new { imageId = img.Id, url = img.Url, status = (int)resp.StatusCode });
                    continue;
                }
                var mime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                if (mime is not "image/jpeg" and not "image/png" and not "image/webp" and not "image/gif")
                {
                    errors.Add(new { imageId = img.Id, error = $"unsupported mime {mime}" });
                    continue;
                }
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > 10 * 1024 * 1024)
                {
                    errors.Add(new { imageId = img.Id, error = "exceeds 10MB" });
                    continue;
                }
                var keyPrefix = $"products/{img.TenantId}/{img.ProductId}/{Guid.NewGuid():N}";
                var optimized = await _imageOpt.OptimizeAsync(bytes, mime, keyPrefix, ct);
                img.Url480w = optimized.Url480w;
                img.Url1024w = optimized.Url1024w;
                img.Url2048w = optimized.Url2048w;
                img.BlurDataUrl = optimized.BlurDataUrl;
                img.Width = optimized.Width;
                img.Height = optimized.Height;
                img.OptimizedAt = DateTime.UtcNow;
                processed++;
                touchedProducts.Add(img.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falhou optimização legacy imageId={Id} url={Url}", img.Id, img.Url);
                errors.Add(new { imageId = img.Id, error = ex.Message });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Dispara webhook por produto tocado (não por imagem — evita N x publishs do mesmo produto).
        foreach (var pid in touchedProducts)
        {
            try { await _service.RepublishWebhookAsync(pid, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Falhou republish webhook productId={Id}", pid); }
        }

        var remaining = await _db.ProductImages.CountAsync(i => i.OptimizedAt == null && i.Url.StartsWith("http"), ct);
        return Ok(new { processed, remaining, errors });
    }
}

public sealed record ImportMolanoRequest(Guid FornecedorId, string Csv);

/// <summary>Sprint 203: request para detectar colunas CSV via Claude.</summary>
/// <summary>Sprint 209: opcional FornecedorId — se Fornecedor já tem mapping cached, skip Claude.</summary>
public sealed record DetectCsvColumnsRequest(string Csv, Guid? FornecedorId = null);

/// <summary>Sprint 203: import com mapping específico (Bruno confirma após detecção Claude).</summary>
public sealed record ImportCsvWithMappingRequest(
    Guid FornecedorId,
    string Csv,
    RepairDesk.Services.Products.CsvImportMapping Mapping,
    bool SaveMapping);
/// <summary>Sprint 195: input para gerar SEO sem productId (durante criação).</summary>
public sealed record PreviewSeoRequest(
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    /// <summary>Sprint 196b: 'Novo', 'Como novo (Grade A)', 'Excelente', 'Bom', 'Aceitável', 'Open Box', etc. Vem do mapper PT.</summary>
    string? Condition,
    string? ImageUrl);
public sealed record MigrateShopRequest(IReadOnlyList<MigrateShopProductRequest> Products);
public sealed record GenerateSeoResponse(
    string SeoTitle,
    string SeoDescription,
    string Alt,
    string DescriptionMarkdown,
    /// <summary>Sprint 199: JSON string com specs estruturadas. Null se Claude não gerou.</summary>
    string? AttributesJson,
    int ImagesUpdated);
