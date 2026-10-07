using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Vendas;

namespace RepairDesk.API.Controllers;

/// <summary>Vendas unificadas — reparação, serviço ou produto (Doc 94 Fase 4).</summary>
[ApiController]
[Route("api/vendas")]
[Authorize]
public class VendasController : ControllerBase
{
    private readonly IVendaService _service;
    private readonly IVendaPdfService _pdf;

    public VendasController(IVendaService service, IVendaPdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    [HttpGet]
    public Task<PagedResult<VendaDto>> Search(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? clienteId,
        [FromQuery] VendaTipo? tipo,
        [FromQuery] VendaEstado? estado,
        [FromQuery] bool emCurso = false,
        [FromQuery] bool faturaPorRegistar = false,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _service.SearchAsync(new VendaFiltro(from, to, clienteId, tipo, estado, emCurso, faturaPorRegistar, q), page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<VendaDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpGet("imei-lookup/{imei}")]
    public async Task<ActionResult<VendaImeiLookupDto>> ImeiLookup(string imei, CancellationToken ct)
    {
        var result = await _service.ImeiLookupAsync(imei, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Reparações cujo IMEI bate com itens desta venda (criadas depois) — o equipamento voltou?</summary>
    [HttpGet("{id:guid}/reparacoes-relacionadas")]
    public Task<IReadOnlyList<VendaReparacaoRelacionadaDto>> ReparacoesRelacionadas(Guid id, CancellationToken ct)
        => _service.GetReparacoesRelacionadasAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<VendaDto>> Create([FromBody] VendaWriteRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<VendaDto> Update(Guid id, [FromBody] VendaWriteRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    /// <summary>Muda o estado (Orçamento → À espera de peça → Pronta → Entregue). Cancelar repõe o stock — só Admin.</summary>
    [HttpPost("{id:guid}/estado")]
    public async Task<ActionResult<VendaDto>> MudarEstado(Guid id, [FromBody] MudarEstadoVendaRequest req, CancellationToken ct)
    {
        if (req.Estado == VendaEstado.Cancelada && !User.IsInRole("Admin"))
            return Forbid();
        return await _service.MudarEstadoAsync(id, req, ct);
    }

    /// <summary>Regista o nº da fatura emitida no programa de faturação (Moloni).</summary>
    [HttpPut("{id:guid}/fatura")]
    public Task<VendaDto> RegistarFatura(Guid id, [FromBody] RegistarFaturaRequest req, CancellationToken ct)
        => _service.RegistarFaturaAsync(id, req, ct);

    [HttpGet("{id:guid}/recibo.pdf")]
    public async Task<IActionResult> ReciboPdf(Guid id, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (pdf, filename) = await _pdf.ForVendaAsync(id, baseUrl, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Vendas entregues em CSV (Excel, UTF-8 com BOM), com IVA a pagar e lucro.</summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(from, to, ct);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return File(bytes, "text/csv; charset=utf-8", $"vendas_{stamp}.csv");
    }
}
