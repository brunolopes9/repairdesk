using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RepairDesk.Services.Products;

/// <summary>
/// Sprint 189: pipeline imagens SEO (Contexto/60). Toma original (PNG/JPG arbitrário) e
/// produz 3 versões WebP (480w, 1024w, 2048w) + blur LQIP base64 + dimensões.
/// Upload para R2 via IPhotoStorage. URLs públicas devolvidas para gravar no ProductImage.
///
/// AVIF ficou para iteração futura (ImageSharp não tem encoder built-in — precisa
/// de Magick.NET ou outro). WebP cobre ~96% browsers.
/// </summary>
public interface IImageOptimizationService
{
    Task<OptimizedImageResult> OptimizeAsync(byte[] originalBytes, string originalContentType, string keyPrefix, CancellationToken ct = default);
}

public sealed record OptimizedImageResult(
    string OriginalUrl,
    string Url480w,
    string Url1024w,
    string Url2048w,
    string BlurDataUrl,
    int Width,
    int Height);

public sealed class ImageOptimizationService : IImageOptimizationService
{
    private readonly IPhotoStorage _storage;
    private readonly ILogger<ImageOptimizationService> _logger;
    private readonly string _publicBaseUrl;

    private static readonly int[] Widths = { 480, 1024, 2048 };

    public ImageOptimizationService(IPhotoStorage storage, IConfiguration config, ILogger<ImageOptimizationService> logger)
    {
        _storage = storage;
        _logger = logger;
        // Sprint 189: URL pública para construir links CDN. Em R2 com custom domain
        // (cdn.lopestech.pt) é o domain root; em local fica /photos servido pelo PhotoController.
        _publicBaseUrl = (config["PHOTOS_PUBLIC_BASE_URL"] ?? "/photos").TrimEnd('/');
    }

    public async Task<OptimizedImageResult> OptimizeAsync(byte[] originalBytes, string originalContentType, string keyPrefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(originalBytes);
        if (originalBytes.Length == 0) throw new ArgumentException("Empty bytes", nameof(originalBytes));

        keyPrefix = keyPrefix.TrimEnd('/');
        using var image = Image.Load(originalBytes);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // 1. Upload original (mantém para histórico/fallback).
        var originalExt = GuessExt(originalContentType);
        var originalKey = $"{keyPrefix}/original.{originalExt}";
        using (var ms = new MemoryStream(originalBytes))
        {
            await _storage.UploadAsync(originalKey, ms, originalContentType, ct);
        }

        // 2. Gerar 3 versões WebP.
        var urls = new Dictionary<int, string>();
        foreach (var targetWidth in Widths)
        {
            // Se a original já é mais pequena, não upscale — usa o tamanho original.
            var width = Math.Min(targetWidth, originalWidth);
            urls[targetWidth] = await EncodeAndUploadWebpAsync(image, width, keyPrefix, ct);
        }

        // 3. Blur LQIP: imagem 16w extra-compressa, base64 inline.
        var blurDataUrl = GenerateBlurPlaceholder(image);

        return new OptimizedImageResult(
            OriginalUrl: PublicUrl(originalKey),
            Url480w: urls[480],
            Url1024w: urls[1024],
            Url2048w: urls[2048],
            BlurDataUrl: blurDataUrl,
            Width: originalWidth,
            Height: originalHeight);
    }

    private async Task<string> EncodeAndUploadWebpAsync(Image source, int targetWidth, string keyPrefix, CancellationToken ct)
    {
        using var clone = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(targetWidth, 0), // height = 0 → keep aspect ratio
            Mode = ResizeMode.Max,
        }));

        using var ms = new MemoryStream();
        var encoder = new WebpEncoder
        {
            Quality = 82,
            FileFormat = WebpFileFormatType.Lossy,
            Method = WebpEncodingMethod.Default,
        };
        await clone.SaveAsync(ms, encoder, ct);
        ms.Position = 0;

        var key = $"{keyPrefix}/{targetWidth}w.webp";
        await _storage.UploadAsync(key, ms, "image/webp", ct);
        return PublicUrl(key);
    }

    private static string GenerateBlurPlaceholder(Image source)
    {
        // 16w blur compressed JPEG embedded as data URL (~1-2 KB).
        // Frontend usa como background até a imagem real carregar.
        using var clone = source.Clone(ctx => ctx
            .Resize(new ResizeOptions { Size = new Size(16, 0), Mode = ResizeMode.Max })
            .GaussianBlur(2f));
        using var ms = new MemoryStream();
        clone.Save(ms, new JpegEncoder { Quality = 30 });
        return $"data:image/jpeg;base64,{Convert.ToBase64String(ms.ToArray())}";
    }

    private string PublicUrl(string key) => $"{_publicBaseUrl}/{key.TrimStart('/')}";

    private static string GuessExt(string mime) => mime switch
    {
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        "image/gif" => "gif",
        _ => "bin",
    };
}
