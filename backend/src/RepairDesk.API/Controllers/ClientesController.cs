using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _service;
    private readonly IClienteRgpdService _rgpd;

    public ClientesController(IClienteService service, IClienteRgpdService rgpd)
    {
        _service = service;
        _rgpd = rgpd;
    }

    [HttpGet]
    public Task<PagedResult<ClienteDto>> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? tagId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(q, page, pageSize, tagId, ct);

    [HttpGet("{id:guid}")]
    public Task<ClienteDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpGet("{id:guid}/equipamentos")]
    public Task<IReadOnlyList<ClienteEquipamentoDto>> Equipamentos(
        Guid id,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
        => _service.ListEquipamentosAsync(id, take, ct);

    /// <summary>
    /// Sprint 453: histórico de comunicações com este cliente (todas as reparações).
    /// Eixo cliente da feature S452. Útil para ver "o que se passou com este cliente
    /// nos últimos N contactos" sem ter que abrir cada reparação.
    /// </summary>
    [HttpGet("{id:guid}/comunicacoes")]
    public async Task<ActionResult<IReadOnlyList<RepairDesk.Services.Comunicacoes.ReparacaoComunicacaoDto>>> Comunicacoes(
        Guid id,
        [FromQuery] int take = 50,
        [FromServices] RepairDesk.Services.Comunicacoes.IReparacaoComunicacaoService comunicacoes = null!,
        CancellationToken ct = default)
        => Ok(await comunicacoes.ListByClienteAsync(id, take, ct));

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create([FromBody] CreateClienteRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Find-by-NIF-or-create atómico. Para integrações externas (loja online, importadores) que
    /// querem garantir cliente sem causar duplicados. Devolve cliente + flag <c>created</c>.
    /// Sem NIF, cria sempre.
    /// </summary>
    [HttpPost("lookup-or-create")]
    public Task<LookupOrCreateClienteResponse> LookupOrCreate([FromBody] CreateClienteRequest req, CancellationToken ct)
        => _service.LookupOrCreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<ClienteDto> Update(Guid id, [FromBody] UpdateClienteRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    /// <summary>Sprint 480: replaces the full customer segment tag set.</summary>
    [HttpPut("{id:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<ClienteTagSummaryDto>>> SetTags(
        Guid id,
        [FromBody] SetClienteTagsRequest req,
        [FromServices] IClienteTagRepository tagRepo,
        CancellationToken ct)
    {
        _ = await _service.GetAsync(id, ct);
        await tagRepo.SetTagsForClienteAsync(id, req.TagIds ?? Array.Empty<Guid>(), ct);
        var tags = await tagRepo.ListByClienteAsync(id, ct);
        return Ok(tags.Select(t => new ClienteTagSummaryDto(t.Id, t.Nome, t.CorHex)).ToList());
    }

    // Sprint 244 Fase B: soft-delete cliente esconde histórico (vendas, reparações) das
    // listas. Hard-delete já é Admin. Import bulk insere dados em massa. Doc 72 §2.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Importa clientes a partir de CSV. Header obrigatório: nome[,telefone,email,nif,notas].
    /// Separador auto-detectado (vírgula/ponto-e-vírgula/tab). Dedupe por NIF.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public Task<ImportClientesResponse> Import([FromBody] ImportClientesRequest req, CancellationToken ct)
        => _service.ImportCsvAsync(req.Csv, ct);

    /// <summary>Exporta todos os clientes do tenant em CSV (UTF-8 BOM, Excel-friendly).</summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(ct);
        var filename = $"clientes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    [HttpGet("{id:guid}/exportar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportarCliente(Guid id, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var dto = await _rgpd.ExportAsync(id, baseUrl, ct);
        var filename = $"cliente_{id:N}_rgpd.json";
        return File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(dto, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        }), "application/json; charset=utf-8", filename);
    }

    [HttpDelete("{id:guid}/hard-delete")]
    [Authorize(Roles = "Admin")]
    public Task<HardDeleteClienteResponse> HardDelete(Guid id, [FromBody] HardDeleteClienteRequest req, CancellationToken ct)
        => _rgpd.HardDeleteAsync(id, req, ct);
}
