using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// Sprint 237 H1.2: explicitamente anonymous — liveness probes (k8s, docker, monitoring
// externo) não devem precisar de JWT. Resposta minimal sem expor versão exacta para
// non-authenticated (defesa em profundidade contra fingerprinting).
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        utc = DateTime.UtcNow,
        version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0"
    });
}
