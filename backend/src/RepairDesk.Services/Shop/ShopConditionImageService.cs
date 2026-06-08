using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Products;

namespace RepairDesk.Services.Shop;

/// <summary>
/// Sprint 531: gere as 4 imagens ilustrativas por estado de condição (A+/A/B+/B) que a loja online
/// mostra no seletor visual estilo Swappie. Mender = single source of truth — a loja só consome.
/// </summary>
public interface IShopConditionImageService
{
    Task<IReadOnlyList<ShopConditionImageDto>> ListAsync(CancellationToken ct = default);
    Task<ShopConditionImageDto> SetAsync(string grade, byte[] bytes, string contentType, string? alt, CancellationToken ct = default);
    Task DeleteAsync(string grade, CancellationToken ct = default);
}

public sealed record ShopConditionImageDto(
    string Grade,
    string Url,
    string? Url480w,
    string? Url1024w,
    string? Url2048w,
    string? BlurDataUrl,
    string? Alt,
    int Width,
    int Height);

public sealed class ShopConditionImageService : IShopConditionImageService
{
    // Graus de loja (slugs alinhados com ProductGradingMapper.GradeSlug). A++→a-plus já é tratado no payload.
    public static readonly string[] ValidGrades = { "a-plus", "a", "b-plus", "b" };

    private readonly IShopConditionImageRepository _repo;
    private readonly IImageOptimizationService _imageOpt;

    public ShopConditionImageService(IShopConditionImageRepository repo, IImageOptimizationService imageOpt)
    {
        _repo = repo;
        _imageOpt = imageOpt;
    }

    public async Task<IReadOnlyList<ShopConditionImageDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(ct);
        return rows.OrderBy(r => Array.IndexOf(ValidGrades, r.Grade)).Select(Map).ToList();
    }

    public async Task<ShopConditionImageDto> SetAsync(string grade, byte[] bytes, string contentType, string? alt, CancellationToken ct = default)
    {
        grade = Normalize(grade);
        if (bytes is null || bytes.Length == 0)
            throw new ValidationException("image_empty", "Imagem vazia.");

        var opt = await _imageOpt.OptimizeAsync(bytes, contentType, $"shop-condition/{grade}", ct);

        var existing = await _repo.FindByGradeAsync(grade, ct);
        if (existing is null)
        {
            existing = new ShopConditionImage { Grade = grade, Url = opt.OriginalUrl };
            await _repo.AddAsync(existing, ct);
        }

        existing.Url = opt.OriginalUrl;
        existing.Url480w = opt.Url480w;
        existing.Url1024w = opt.Url1024w;
        existing.Url2048w = opt.Url2048w;
        existing.BlurDataUrl = opt.BlurDataUrl;
        existing.Width = opt.Width;
        existing.Height = opt.Height;
        if (alt is not null) existing.Alt = alt.Trim().Length > 300 ? alt.Trim()[..300] : alt.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveAsync(ct);
        return Map(existing);
    }

    public async Task DeleteAsync(string grade, CancellationToken ct = default)
    {
        grade = Normalize(grade);
        var existing = await _repo.FindByGradeAsync(grade, ct);
        if (existing is null) return;
        // Hard delete: o índice único (TenantId, Grade) não pode ficar ocupado por linha soft-deleted.
        _repo.Remove(existing);
        await _repo.SaveAsync(ct);
    }

    private static string Normalize(string grade)
    {
        var g = (grade ?? string.Empty).Trim().ToLowerInvariant();
        if (!ValidGrades.Contains(g))
            throw new ValidationException("grade_invalid", $"Grau inválido. Use um de: {string.Join(", ", ValidGrades)}.");
        return g;
    }

    private static ShopConditionImageDto Map(ShopConditionImage x)
        => new(x.Grade, x.Url, x.Url480w, x.Url1024w, x.Url2048w, x.BlurDataUrl, x.Alt, x.Width, x.Height);
}
