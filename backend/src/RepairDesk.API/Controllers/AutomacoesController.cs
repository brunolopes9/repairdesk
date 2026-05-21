using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 165: pequenos utilitários para a página /definicoes/automacoes do frontend.
/// Por agora só o status do n8n — faz ping server-side para evitar ERR_CONNECTION_REFUSED
/// no devtools console quando n8n não está a correr (profile docker compose automation).
/// </summary>
[ApiController]
[Route("api/automacoes")]
[Authorize]
public class AutomacoesController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public AutomacoesController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    [HttpGet("n8n-status")]
    public async Task<IActionResult> N8nStatus(CancellationToken ct)
    {
        // Em docker compose, o serviço n8n está acessível via DNS interno "n8n:5678".
        // Em dev (sem container automation a correr) pode estar em localhost.
        var url = _config["N8N_HEALTH_URL"] ?? "http://n8n:5678/healthz";
        try
        {
            using var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            using var res = await http.GetAsync(url, ct);
            return Ok(new { up = res.IsSuccessStatusCode, url });
        }
        catch
        {
            return Ok(new { up = false, url });
        }
    }
}
