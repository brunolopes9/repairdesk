using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.Controllers;

/// <summary>Sprint 661: subscritores da newsletter da loja — só o dono (Admin).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/loja/newsletter")]
public sealed class LojaNewsletterController : ControllerBase
{
    private readonly INewsletterService _service;
    public LojaNewsletterController(INewsletterService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<NewsletterResumoDto>> Resumo(CancellationToken ct) => Ok(await _service.ResumoAsync(ct));
}
