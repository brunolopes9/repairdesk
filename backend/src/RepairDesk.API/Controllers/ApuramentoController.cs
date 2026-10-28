using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 573: IVA e resultado por trimestre (dados financeiros — só o administrador).</summary>
[ApiController]
[Route("api/apuramento")]
[Authorize(Roles = "Admin")]
public class ApuramentoController : ControllerBase
{
    private readonly IApuramentoService _service;
    public ApuramentoController(IApuramentoService service) => _service = service;

    [HttpGet]
    public Task<ApuramentoDto> Trimestre([FromQuery] int ano, [FromQuery] int trimestre, CancellationToken ct = default)
        => _service.TrimestreAsync(ano, trimestre, ct);
}
