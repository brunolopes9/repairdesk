using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Files;
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
        [FromQuery] bool porPagar = false,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null,
        CancellationToken ct = default)
        => _service.SearchAsync(new VendaFiltro(from, to, clienteId, tipo, estado, emCurso, faturaPorRegistar, q, Ordenacao.De(sort, dir), porPagar), page, pageSize, ct);

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

    /// <summary>Sprint 563: corrige preços/IVA de uma venda entregue (erro de lançamento). Só Admin; auditado.</summary>
    [HttpPut("{id:guid}/corrigir-precos")]
    [Authorize(Roles = "Admin")]
    public Task<VendaDto> CorrigirPrecos(Guid id, [FromBody] CorrigirPrecosRequest req, CancellationToken ct)
        => _service.CorrigirPrecosAsync(id, req, ct);

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

    /// <summary>
    /// Sprint 565: importa uma fatura emitida no Moloni (PDF) — cria a venda entregue com a fatura, ou
    /// anexa o PDF à venda que já tem esse nº. Idempotente.
    /// </summary>
    [HttpPost("importar-fatura")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<VendaFaturaImportResult>> ImportarFatura(
        [FromForm] IFormFile file, [FromServices] IVendaFaturaService faturas, [FromServices] IFileValidator validator, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "no_file", detail = "Anexa o PDF da fatura." });
        await using var stream = file.OpenReadStream();
        var validated = await validator.ValidateAsync(stream, file.ContentType, FileKind.Pdf, ct);
        return Ok(await faturas.ImportarAsync(validated.Buffer, ct));
    }

    /// <summary>Sprint 569: talão de 80 mm (impressora de talões do balcão).</summary>
    [HttpGet("{id:guid}/talao.pdf")]
    public async Task<IActionResult> TalaoPdf(Guid id, [FromServices] ITalaoPdfService talao, CancellationToken ct)
        => File(await talao.GerarAsync(id, ct), "application/pdf", "talao.pdf");

    /// <summary>Sprint 565: o PDF da fatura de venda (se foi importado).</summary>
    [HttpGet("{id:guid}/fatura.pdf")]
    public async Task<IActionResult> FaturaPdf(Guid id, [FromServices] IVendaFaturaService faturas, CancellationToken ct)
    {
        var (pdf, nome) = await faturas.PdfAsync(id, ct);
        return File(pdf, "application/pdf", nome);
    }

    /// <summary>Sprint 565: ZIP das faturas de venda do período (pela data da fatura) + indice.csv — para o contabilista.</summary>
    [HttpGet("faturas/export")]
    public async Task<IActionResult> ExportFaturas([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromServices] IVendaFaturaService faturas, CancellationToken ct)
    {
        var (zip, nome) = await faturas.ExportZipAsync(from, to, ct);
        return File(zip, "application/zip", nome);
    }

    // ---------- Sprint 567: Moloni ON ----------

    /// <summary>Emite a fatura ou a fatura-recibo da venda no Moloni ON (rascunho, confere total, finaliza).</summary>
    [HttpPost("{id:guid}/moloni/emitir")]
    public Task<VendaDto> MoloniEmitir(Guid id, [FromBody] RepairDesk.Services.Moloni.MoloniEmitirRequest req,
        [FromServices] RepairDesk.Services.Moloni.IMoloniService moloni, CancellationToken ct) => moloni.EmitirAsync(id, req, ct);

    /// <summary>Converte a fatura (por pagar) em paga: emite o recibo no Moloni ON.</summary>
    [HttpPost("{id:guid}/moloni/recibo")]
    public Task<VendaDto> MoloniRecibo(Guid id, [FromBody] RepairDesk.Services.Moloni.MoloniReciboRequest req,
        [FromServices] RepairDesk.Services.Moloni.IMoloniService moloni, CancellationToken ct) => moloni.ReciboAsync(id, req, ct);

    /// <summary>Anula no Moloni ON (recibo primeiro, depois a fatura). A venda volta a "Fatura por registar".</summary>
    [HttpPost("{id:guid}/moloni/anular")]
    [Authorize(Roles = "Admin")]
    public Task<VendaDto> MoloniAnular(Guid id, [FromBody] RepairDesk.Services.Moloni.MoloniAnularRequest req,
        [FromServices] RepairDesk.Services.Moloni.IMoloniService moloni, CancellationToken ct) => moloni.AnularAsync(id, req, ct);

    [HttpPost("{id:guid}/moloni/orcamento")]
    public Task<VendaDto> MoloniOrcamento(Guid id, [FromBody] RepairDesk.Services.Moloni.MoloniOrcamentoRequest req,
        [FromServices] RepairDesk.Services.Moloni.IMoloniService moloni, CancellationToken ct) => moloni.OrcamentoAsync(id, req, ct);

    /// <summary>PDF de um documento Moloni da venda: documento = fatura | recibo | orcamento.</summary>
    [HttpGet("{id:guid}/moloni/{documento}.pdf")]
    public async Task<IActionResult> MoloniPdf(Guid id, string documento, [FromServices] RepairDesk.Services.Moloni.IMoloniService moloni, CancellationToken ct)
    {
        var (pdf, nome) = await moloni.PdfAsync(id, documento, ct);
        return File(pdf, "application/pdf", nome);
    }

    public sealed record SaveAssinaturaRequest(string Tipo, string DataUrl);
}
