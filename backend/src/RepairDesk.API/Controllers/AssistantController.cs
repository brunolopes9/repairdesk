using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.API.Assistant;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 369: assistente interno (read-only) — qualquer empregado autenticado pode perguntar.
/// </summary>
[ApiController]
[Authorize]
[Route("api/assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistant;

    public AssistantController(IAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswer>> Ask([FromBody] AssistantAskRequest request, CancellationToken ct)
        => Ok(await _assistant.AskAsync(request, ct));
}
