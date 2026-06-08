using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Auth;
using RepairDesk.Services.Documentos;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 513: "Documentos" — lista única das faturas/documentos emitidos, o backbone do separador
/// Vendas em Compras e Operação. Mender como sítio único onde se vêem todas as faturas.
/// </summary>
[ApiController]
[Route("api/documentos")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentoService _service;
    public DocumentosController(IDocumentoService service) => _service = service;

    /// <summary>Lista os documentos de venda (Fatura / Fatura Simplificada / …) emitidos no período.</summary>
    [HttpGet("vendas")]
    public Task<DocumentosListDto> ListVendas(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? q,
        [FromQuery] string? tipo,
        CancellationToken ct)
        => _service.ListVendasAsync(new DocumentosFiltro(from, to, q, tipo), ct);

    /// <summary>Export CSV dos documentos de venda — para o contabilista.</summary>
    [HttpGet("vendas/export.csv")]
    public async Task<IActionResult> ExportVendas(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var bytes = await _service.ExportVendasCsvAsync(from, to, ct);
        return File(bytes, "text/csv", $"documentos-vendas-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Sprint 527: emite um Recibo Moloni que liquida uma Fatura a crédito (em dívida 100%).
    /// Admin-only (escrita fiscal). documentId = id Moloni do documento (DocumentoDto.ExternalId).
    /// </summary>
    [HttpPost("{documentId:int}/recibo")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    public Task<ReciboResultDto> EmitirRecibo(int documentId, CancellationToken ct)
        => _service.EmitirReciboAsync(documentId, ct);
}
