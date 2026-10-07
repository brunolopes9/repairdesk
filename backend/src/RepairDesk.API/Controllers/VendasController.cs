using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Reparacoes;
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
    private readonly IOrcamentoPdfService _orcamento;

    public VendasController(IVendaService service, IVendaPdfService pdf, IOrcamentoPdfService orcamento)
    {
        _service = service;
        _pdf = pdf;
        _orcamento = orcamento;
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

    /// <summary>Orçamento em PDF para enviar ao cliente (não é documento fiscal).</summary>
    [HttpGet("{id:guid}/orcamento.pdf")]
    public async Task<IActionResult> OrcamentoPdf(Guid id, CancellationToken ct)
    {
        var (pdf, filename) = await _orcamento.ForVendaAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Reparação: etiqueta 62×29mm com QR para o portal (impressora térmica).</summary>
    [HttpGet("{id:guid}/label.pdf")]
    public async Task<IActionResult> LabelPdf(Guid id, [FromServices] ILabelPdfService labels, CancellationToken ct)
    {
        var (pdf, filename) = await labels.ForVendaAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Reparação: "Comprovativo de entrada" do equipamento (com assinatura do cliente, se recolhida).</summary>
    [HttpGet("{id:guid}/entrada.pdf")]
    public async Task<IActionResult> EntradaPdf(Guid id, [FromServices] IEntradaPdfService entrada, CancellationToken ct)
    {
        var (pdf, filename) = await entrada.ForVendaAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Reparação: "Recibo de entrega" do equipamento (não é fatura).</summary>
    [HttpGet("{id:guid}/entrega.pdf")]
    public async Task<IActionResult> EntregaPdf(Guid id, [FromServices] IEntregaPdfService entrega, CancellationToken ct)
    {
        var (pdf, filename) = await entrega.ForVendaAsync(id, ct);
        return File(pdf, "application/pdf", filename);
    }

    /// <summary>Assinatura do cliente (canvas → PNG) na entrada ou entrega. Recapturar substitui.</summary>
    [HttpPost("{id:guid}/assinaturas")]
    public Task<AssinaturaDto> SaveAssinatura(
        Guid id, [FromBody] SaveAssinaturaRequest req, [FromServices] IAssinaturaService assinaturas, CancellationToken ct)
        => assinaturas.SaveAsync(id, req.Tipo, req.DataUrl, ct);

    [HttpGet("{id:guid}/assinaturas")]
    public Task<IReadOnlyList<AssinaturaDto>> ListAssinaturas(
        Guid id, [FromServices] IAssinaturaService assinaturas, CancellationToken ct)
        => assinaturas.ListAsync(id, ct);

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

    public sealed record SaveAssinaturaRequest(string Tipo, string DataUrl);
}
