using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 147: endpoint para n8n IMAP workflow submeter PDFs de fornecedor.
/// Auth: API key com scope "ingest" (não dá acesso a leituras de catálogo/orders).
///
/// Workflow típico:
///   1. n8n IMAP Trigger pega novo email com PDF anexo na pasta "Fornecedores"
///   2. POST /api/external/supplier-invoices/ingest com PDF base64 + emailMeta
///   3. Backend faz SHA256 dedup, corre SupplierPdfParser, guarda PDF em
///      {storage-root}/{tenantId}/{ano}/{mes}/{fornecedor}/{filename}.pdf
///   4. Cria SupplierInvoiceImport em Status=Pending; Bruno revê na UI
///   5. n8n move email para label "Fornecedores/{supplier}/{ano-mes}" se sucesso
/// </summary>
[ApiController]
[Route("api/external/supplier-invoices")]
[Authorize(AuthenticationSchemes = ApiKeyAuthHandler.SchemeName)]
[EnableRateLimiting("external-apikey")]
public class ExternalSupplierInvoicesController : ControllerBase
{
    private readonly ISupplierInvoiceImportService _service;

    public ExternalSupplierInvoicesController(ISupplierInvoiceImportService service) => _service = service;

    /// <summary>Recebe um PDF + metadata do email. Devolve resultado do parse (já guardado).</summary>
    [HttpPost("ingest")]
    [ApiScope("ingest")]
    public async Task<ActionResult<SupplierInvoiceImportResult>> Ingest(
        [FromBody] SupplierInvoiceIngestRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PdfBase64))
            return UnprocessableEntity(new { code = "pdf_required", detail = "PDF base64 obrigatório." });

        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(req.PdfBase64);
        }
        catch (FormatException)
        {
            return UnprocessableEntity(new { code = "pdf_base64_invalid", detail = "PDF base64 inválido." });
        }

        // Sanity: PDFs costumam ter pelo menos alguns KB; >25MB rejeita preventivamente.
        if (pdfBytes.Length < 100 || pdfBytes.Length > 25 * 1024 * 1024)
            return UnprocessableEntity(new { code = "pdf_size_invalid", detail = "PDF tem tamanho inválido (mín 100B, máx 25MB)." });

        var meta = req.EmailMeta is null
            ? null
            : new SupplierInvoiceEmailMeta(
                req.EmailMeta.MessageId, req.EmailMeta.Subject,
                req.EmailMeta.From, req.EmailMeta.ReceivedAt);

        var apiKeyIdClaim = User.FindFirst("api_key_id")?.Value;
        Guid? apiKeyId = Guid.TryParse(apiKeyIdClaim, out var k) ? k : null;

        var result = await _service.IngestAsync(pdfBytes, meta, apiKeyId, ct);

        // Sprint 147: status code distinto para duplicado — n8n pode tratar diferente do create real.
        if (result.WasDuplicate) return Ok(result);
        return CreatedAtAction(nameof(Ingest), null, result);
    }

    /// <summary>
    /// Lista importações pendentes de revisão. Útil para n8n confirmar que o ingest entrou e para
    /// a UI Definições mostrar inbox.
    /// </summary>
    [HttpGet("pending")]
    [ApiScope("ingest")]
    public Task<IReadOnlyList<SupplierInvoiceImportDto>> ListPending(
        [FromQuery] int take = 50, CancellationToken ct = default)
        => _service.ListPendingAsync(take, ct);
}

public sealed class SupplierInvoiceIngestRequest
{
    public string PdfBase64 { get; set; } = "";
    public SupplierInvoiceIngestEmailMeta? EmailMeta { get; set; }
}

public sealed class SupplierInvoiceIngestEmailMeta
{
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    public string? From { get; set; }
    public DateTime? ReceivedAt { get; set; }
}
