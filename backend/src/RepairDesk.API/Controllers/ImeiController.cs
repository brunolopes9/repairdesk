using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Imei;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 390 (Doc 04): lookup TAC→modelo (auto-detetar marca/modelo a partir do IMEI) + import
/// da base TAC. Lookup é operacional (qualquer empregado); o import da base é só Admin.
/// </summary>
[ApiController]
[Route("api/imei")]
[Authorize]
public sealed class ImeiController : ControllerBase
{
    private readonly ITacLookupService _tac;
    private readonly AppDbContext _db;
    public ImeiController(ITacLookupService tac, AppDbContext db)
    {
        _tac = tac;
        _db = db;
    }

    /// <summary>Resolve marca+modelo de um IMEI. found=false quando a base não tem o TAC.</summary>
    [HttpGet("lookup")]
    public ActionResult<TacLookupResult> Lookup([FromQuery] string imei)
        => Ok(_tac.Resolve(imei));

    /// <summary>Estado da base TAC (nº de entradas carregadas).</summary>
    [HttpGet("tac-db/status")]
    public ActionResult<object> Status() => Ok(new { count = _tac.Count });

    /// <summary>
    /// Importa a base TAC de um CSV "tac;marca;modelo" (uma linha por TAC). Substitui a base atual.
    /// Usar o dump aberto Osmocom (CC-BY-SA) ou MoazEb (MIT) convertido para este formato.
    /// </summary>
    [HttpPost("tac-db/import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Ficheiro vazio." });
        await using var stream = file.OpenReadStream();
        var count = await _tac.ImportCsvAsync(stream, ct);
        return Ok(new { count });
    }

    /// <summary>
    /// Sprint 391 (Doc 04): export da lista de IMEI de equipamentos vendidos/usados num período, para
    /// enviar à PSP (retalhistas de usados em PT enviam listas periódicas; não há API — é processo
    /// manual). CSV imei;data;equipamento;condicao;fornecedor;venda. Operacional.
    /// </summary>
    [HttpGet("psp-export.csv")]
    public async Task<IActionResult> PspExport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var fromUtc = (from ?? DateTime.UtcNow.AddDays(-30)).ToUniversalTime();
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();

        var items = await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Imei != null && i.Venda!.Data >= fromUtc && i.Venda.Data <= toUtc)
            .OrderBy(i => i.Venda!.Data)
            .Select(i => new
            {
                i.Imei,
                i.Imei2,
                Data = i.Venda!.Data,
                Numero = i.Venda.Numero,
                i.Descricao,
                i.Condicao,
                i.FornecedorNome,
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("imei;data;equipamento;condicao;fornecedor;venda");
        foreach (var i in items)
        {
            void Row(string? imei)
            {
                if (string.IsNullOrWhiteSpace(imei)) return;
                sb.Append(Clean(imei)).Append(';')
                  .Append(i.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
                  .Append(Clean(i.Descricao)).Append(';')
                  .Append(i.Condicao).Append(';')
                  .Append(Clean(i.FornecedorNome)).Append(';')
                  .Append('#').Append(i.Numero.ToString("D5", CultureInfo.InvariantCulture))
                  .Append('\n');
            }
            Row(i.Imei);
            Row(i.Imei2);
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var name = $"psp-imei_{fromUtc:yyyyMMdd}_{toUtc:yyyyMMdd}.csv";
        return File(bytes, "text/csv", name);
    }

    private static string Clean(string? s) => string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ').Trim();
}
