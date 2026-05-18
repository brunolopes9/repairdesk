using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Fotos;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/reparacoes/{reparacaoId:guid}/fotos")]
[Authorize]
public class FotosController : ControllerBase
{
    private readonly IFotoService _service;
    public FotosController(IFotoService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<FotoDto>> List(Guid reparacaoId, CancellationToken ct)
        => _service.ListByReparacaoAsync(reparacaoId, ct);

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024 + 8192)] // 10 MB + header overhead
    public async Task<ActionResult<FotoDto>> Upload(
        Guid reparacaoId,
        [FromForm] IFormFile file,
        [FromForm] FotoTipo tipo = FotoTipo.Antes,
        [FromForm] string? legenda = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Ficheiro obrigatório." });
        await using var stream = file.OpenReadStream();
        var dto = await _service.UploadAsync(reparacaoId, stream, file.FileName, file.ContentType, file.Length, tipo, legenda, ct);
        return Ok(dto);
    }
}

[ApiController]
[Route("api/reparacoes/fotos")]
[Authorize]
public class FotosItemController : ControllerBase
{
    private readonly IFotoService _service;
    private readonly IPhotoExportLinkService _photoLinks;
    public FotosItemController(IFotoService service, IPhotoExportLinkService photoLinks)
    {
        _service = service;
        _photoLinks = photoLinks;
    }

    [HttpGet("{fotoId:guid}/content")]
    public async Task<IActionResult> Content(Guid fotoId, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _service.DownloadAsync(fotoId, ct);
        Response.Headers["Cache-Control"] = "private, max-age=86400";
        return File(stream, contentType, fileName);
    }

    [HttpPut("{fotoId:guid}")]
    public Task<FotoDto> Update(Guid fotoId, [FromBody] UpdateFotoRequest req, CancellationToken ct)
        => _service.UpdateAsync(fotoId, req, ct);

    [HttpDelete("{fotoId:guid}")]
    public async Task<IActionResult> Delete(Guid fotoId, CancellationToken ct)
    {
        await _service.DeleteAsync(fotoId, ct);
        return NoContent();
    }

    [HttpGet("{fotoId:guid}/export-content")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportContent(Guid fotoId, [FromQuery] long expires, [FromQuery] string sig, CancellationToken ct)
    {
        _photoLinks.Validate(fotoId, expires, sig);
        var (stream, contentType, fileName) = await _service.DownloadAsync(fotoId, ct);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(stream, contentType, fileName);
    }
}

/// <summary>
/// Endpoint público para servir fotos visíveis no portal cliente. Sem auth,
/// rate-limited.
/// </summary>
[ApiController]
[Route("api/public/repair-photo")]
[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-portal")]
public class PublicFotoController : ControllerBase
{
    private readonly IFotoService _service;
    public PublicFotoController(IFotoService service) => _service = service;

    [HttpGet("{fotoId:guid}")]
    public async Task<IActionResult> Get(Guid fotoId, CancellationToken ct)
    {
        var (stream, contentType) = await _service.DownloadPublicAsync(fotoId, ct);
        Response.Headers["Cache-Control"] = "public, max-age=3600";
        return File(stream, contentType);
    }
}
