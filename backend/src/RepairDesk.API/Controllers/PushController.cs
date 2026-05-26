using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Push;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 366: subscrição de Web Push para dispositivos de STAFF (telemóvel/desktop do
/// utilizador autenticado). A chave VAPID pública é a mesma do portal do cliente, obtida
/// em /api/public/portal/push/vapid-public-key (anónimo, é pública).
/// </summary>
[ApiController]
[Authorize]
[Route("api/push")]
public sealed class PushController : ControllerBase
{
    private readonly IStaffPushService _staffPush;

    public PushController(IStaffPushService staffPush)
    {
        _staffPush = staffPush;
    }

    [HttpPost("subscribe")]
    public async Task<ActionResult<PushSubscriptionResultDto>> Subscribe([FromBody] BrowserPushSubscriptionDto request, CancellationToken ct)
        => Ok(await _staffPush.SubscribeAsync(request, ct));

    [HttpPost("unsubscribe")]
    public async Task<ActionResult<PushSubscriptionResultDto>> Unsubscribe([FromBody] UnsubscribePushRequest request, CancellationToken ct)
        => Ok(await _staffPush.UnsubscribeAsync(request, ct));
}
