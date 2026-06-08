using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Auth;
using RepairDesk.Services.Files;
using RepairDesk.Services.Shop;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 531: admin gere as 4 imagens ilustrativas por estado de condição (A+/A/B+/B) que a loja
/// online mostra no seletor visual. Mender = single source of truth. Só admin.
/// </summary>
[ApiController]
[Route("api/shop-condition-images")]
[Authorize(Policy = AppPolicies.RequireAdmin)]
public class ShopConditionImagesController : ControllerBase
{
    private readonly IShopConditionImageService _service;
    private readonly IFileValidator _fileValidator;

    public ShopConditionImagesController(IShopConditionImageService service, IFileValidator fileValidator)
    {
        _service = service;
        _fileValidator = fileValidator;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpPut("{grade}")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Set(string grade, IFormFile image, [FromForm] string? alt, CancellationToken ct)
    {
        if (image is null || image.Length == 0) return BadRequest(new { code = "no_image" });
        // Sprint 247: valida por magic bytes, não só o MIME do cliente.
        await using var stream = image.OpenReadStream();
        var validated = await _fileValidator.ValidateAsync(stream, image.ContentType, FileKind.Image, ct);
        var dto = await _service.SetAsync(grade, validated.Buffer, validated.DetectedMime, alt, ct);
        return Ok(dto);
    }

    [HttpDelete("{grade}")]
    public async Task<IActionResult> Delete(string grade, CancellationToken ct)
    {
        await _service.DeleteAsync(grade, ct);
        return NoContent();
    }
}
