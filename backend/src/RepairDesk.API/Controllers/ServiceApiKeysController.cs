using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.ServiceApiKeys;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Gestão de chaves de API para integrações externas. Acessível apenas via JWT
/// (utilizadores autenticados Admin) — não via outra chave de API, para evitar
/// escalada de privilégios.
/// </summary>
[ApiController]
[Route("api/service-keys")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class ServiceApiKeysController : ControllerBase
{
    private readonly IServiceApiKeyService _service;

    public ServiceApiKeysController(IServiceApiKeyService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<ServiceApiKeyDto>> List(CancellationToken ct) => _service.ListAsync(ct);

    /// <summary>Cria nova chave. Plain key devolvido apenas neste response — guarda em local seguro.</summary>
    [HttpPost]
    public Task<CreateServiceApiKeyResponse> Create([FromBody] CreateServiceApiKeyRequest req, CancellationToken ct)
        => _service.CreateAsync(req.Name, ct);

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeServiceApiKeyRequest req, CancellationToken ct)
    {
        await _service.RevokeAsync(id, req.Reason, ct);
        return NoContent();
    }
}
