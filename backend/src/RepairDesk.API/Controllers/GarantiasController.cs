using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Garantias;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/garantias")]
[Authorize]
public class GarantiasController : ControllerBase
{
    private readonly IGarantiaService _service;
    public GarantiasController(IGarantiaService service) => _service = service;

    [HttpGet("by-reparacao/{reparacaoId:guid}")]
    public async Task<ActionResult<GarantiaAdminDto>> GetByReparacao(Guid reparacaoId, CancellationToken ct)
    {
        var g = await _service.GetByReparacaoAsync(reparacaoId, ct);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpGet("by-venda/{vendaId:guid}")]
    public async Task<ActionResult<GarantiaAdminDto>> GetByVenda(Guid vendaId, CancellationToken ct)
    {
        var g = await _service.GetByVendaAsync(vendaId, ct);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpPost("{id:guid}/anular")]
    [Authorize(Roles = "Admin")]
    public Task<GarantiaAdminDto> Anular(Guid id, [FromBody] AnularGarantiaRequest req, CancellationToken ct)
        => _service.AnularAsync(id, req.Motivo, ct);

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        // Portal base URL — Host: domínio actual (lopestech.pt em prod, localhost em dev)
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (pdf, filename) = await _service.RenderPdfAsync(id, baseUrl, ct);
        return File(pdf, "application/pdf", filename);
    }
}
