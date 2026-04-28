using Microsoft.AspNetCore.Mvc;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
