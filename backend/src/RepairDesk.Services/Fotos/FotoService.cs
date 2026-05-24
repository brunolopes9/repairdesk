using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Fotos;

public interface IFotoService
{
    Task<IReadOnlyList<FotoDto>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<FotoDto> UploadAsync(Guid reparacaoId, Stream content, string fileName, string contentType, long size, FotoTipo tipo, string? legenda, CancellationToken ct = default);
    Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(Guid fotoId, CancellationToken ct = default);
    Task<FotoDto> UpdateAsync(Guid fotoId, UpdateFotoRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid fotoId, CancellationToken ct = default);

    /// <summary>Lista fotos públicas (visíveis no portal). Usado pelo PublicPortalService.</summary>
    Task<IReadOnlyList<FotoDto>> ListPublicAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<(Stream Content, string ContentType)> DownloadPublicAsync(Guid fotoId, CancellationToken ct = default);
}

public class FotoService : IFotoService
{
    // Configuração: tipos aceites e tamanho máximo
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };
    private const long MaxSize = 10 * 1024 * 1024; // 10 MB

    private readonly IReparacaoFotoRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly IPhotoStorage _storage;
    private readonly ITenantContext _tenant;

    public FotoService(
        IReparacaoFotoRepository repo,
        IReparacaoRepository reparacoes,
        IPhotoStorage storage,
        ITenantContext tenant)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _storage = storage;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FotoDto>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rows = await _repo.ListByReparacaoAsync(reparacaoId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FotoDto> UploadAsync(Guid reparacaoId, Stream content, string fileName, string contentType, long size, FotoTipo tipo, string? legenda, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(contentType))
            throw new ValidationException("file_type_invalid", "Apenas JPEG/PNG/WebP são aceites.");
        if (size > MaxSize)
            throw new ValidationException("file_too_large", $"Ficheiro demasiado grande (máx {MaxSize / (1024 * 1024)} MB).");
        if (size <= 0)
            throw new ValidationException("file_empty", "Ficheiro vazio.");

        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        if (!_tenant.HasTenant)
            throw new ForbiddenException("no_tenant", "Sem contexto de tenant.");

        var extension = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".bin",
            };

        var fotoId = Guid.NewGuid();
        var storageKey = $"tenants/{rep.TenantId}/reparacoes/{rep.Id}/{fotoId}{extension}";
        await _storage.UploadAsync(storageKey, content, contentType, ct);

        var foto = new ReparacaoFoto
        {
            Id = fotoId,
            ReparacaoId = rep.Id,
            StorageKey = storageKey,
            FileName = FileNameSanitizer.Safe(fileName),
            ContentType = contentType,
            Size = size,
            Tipo = tipo,
            Legenda = string.IsNullOrWhiteSpace(legenda) ? null : legenda.Trim(),
            // Default: Antes/Depois ficam visíveis no portal, Durante NÃO
            VisivelNoPortal = tipo != FotoTipo.Durante,
        };
        await _repo.AddAsync(foto, ct);
        await _repo.SaveAsync(ct);
        return ToDto(foto);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(Guid fotoId, CancellationToken ct = default)
    {
        var foto = await _repo.FindByIdAsync(fotoId, ct)
            ?? throw new NotFoundException("Foto", fotoId);
        var stream = await _storage.DownloadAsync(foto.StorageKey, ct);
        return (stream, foto.ContentType, foto.FileName);
    }

    public async Task<FotoDto> UpdateAsync(Guid fotoId, UpdateFotoRequest req, CancellationToken ct = default)
    {
        var foto = await _repo.FindByIdAsync(fotoId, ct)
            ?? throw new NotFoundException("Foto", fotoId);
        foto.Tipo = req.Tipo;
        foto.Ordem = Math.Max(0, req.Ordem);
        foto.Legenda = string.IsNullOrWhiteSpace(req.Legenda) ? null : req.Legenda.Trim();
        foto.VisivelNoPortal = req.VisivelNoPortal;
        await _repo.SaveAsync(ct);
        return ToDto(foto);
    }

    public async Task DeleteAsync(Guid fotoId, CancellationToken ct = default)
    {
        var foto = await _repo.FindByIdAsync(fotoId, ct)
            ?? throw new NotFoundException("Foto", fotoId);
        // Apaga binário primeiro; se DB falhar depois, fica orphan que é ok
        await _storage.DeleteAsync(foto.StorageKey, ct);
        _repo.Remove(foto);
        await _repo.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<FotoDto>> ListPublicAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rows = await _repo.ListPublicByReparacaoIdAsync(reparacaoId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<(Stream Content, string ContentType)> DownloadPublicAsync(Guid fotoId, CancellationToken ct = default)
    {
        // Aqui ignoramos filter de tenant — endpoint público
        // Mas validamos que a foto está marcada como visível
        var foto = await _repo.FindByIdAsync(fotoId, ct);
        if (foto is null || !foto.VisivelNoPortal)
            throw new NotFoundException("Foto", fotoId);
        var stream = await _storage.DownloadAsync(foto.StorageKey, ct);
        return (stream, foto.ContentType);
    }

    private static FotoDto ToDto(ReparacaoFoto f) => new(
        f.Id, f.ReparacaoId, f.FileName, f.ContentType, f.Size,
        f.Tipo, f.Ordem, f.Legenda, f.VisivelNoPortal, f.CreatedAt);

    // Sprint 248: SafeFileName promovido para Common.FileNameSanitizer.Safe.
}
