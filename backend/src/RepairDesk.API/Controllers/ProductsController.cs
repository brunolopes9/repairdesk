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
    public Task<ImportProductsResponse> ImportMolano([FromBody] ImportMolanoRequest req, CancellationToken ct)
        => _service.ImportMolanoCsvAsync(req.Csv, req.FornecedorId, ct);

    /// <summary>
    /// Sprint 155: migração one-off de produtos shop-only (existiam só na loja antes do
    /// single-source-of-truth). Outro Claude gera o JSON via npm run db:export-shop-only.
    /// Upsert por SKU — re-correr é seguro (skip existentes).
    /// </summary>
    [HttpPost("migrate-shop")]
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
            Condition: null,
            ExtraContext: product.SeoTitle);

        var seo = await _seoGen.GenerateAsync(input, imageBytes, imageMime, ct);
        if (seo is null)
            return BadRequest(new { code = "llm_unavailable", detail = "Anthropic API indisponível ou quota esgotada." });

        // Aplicar SEO meta ao produto (alts ficam para o endpoint per-imagem).
        product.SeoTitle = seo.SeoTitle;
        product.SeoDescription = seo.SeoDescription;
        if (string.IsNullOrWhiteSpace(product.DescriptionMarkdown))
            product.DescriptionMarkdown = seo.DescriptionMarkdown;
        await _db.SaveChangesAsync(ct);

        return Ok(new GenerateSeoResponse(
            SeoTitle: seo.SeoTitle,
            SeoDescription: seo.SeoDescription,
            Alt: seo.Alt,
            DescriptionMarkdown: seo.DescriptionMarkdown,
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
            Condition: null,
            ExtraContext: null);

        var alt = await _seoGen.GenerateAltAsync(bytes, mimeType, ctx, ct);
        if (alt is null) return BadRequest(new { code = "llm_unavailable" });

        image.Alt = alt;
        await _db.SaveChangesAsync(ct);
        return Ok(new { alt });
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
}

public sealed record ImportMolanoRequest(Guid FornecedorId, string Csv);
public sealed record MigrateShopRequest(IReadOnlyList<MigrateShopProductRequest> Products);
public sealed record GenerateSeoResponse(
    string SeoTitle,
    string SeoDescription,
    string Alt,
    string DescriptionMarkdown,
    int ImagesUpdated);
