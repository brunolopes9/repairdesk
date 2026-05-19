using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.DAL.Persistence;

public sealed class RelatorioFiscalRepository : IRelatorioFiscalRepository
{
    private readonly AppDbContext _db;

    public RelatorioFiscalRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var reparacoes = await _db.Reparacoes
            .Where(r => r.InvoiceEmittedAt != null && r.InvoiceEmittedAt >= fromUtc && r.InvoiceEmittedAt < toUtc)
            .Select(r => new RelatorioFiscalDocumentoRow(
                r.Id,
                "Reparacao",
                r.Numero,
                r.InvoiceNumber,
                r.InvoiceEmittedAt!.Value,
                r.Cliente != null ? r.Cliente.Nome : null,
                r.PrecoFinalCents ?? r.OrcamentoCents ?? 0))
            .ToListAsync(ct);

        var trabalhos = await _db.Trabalhos
            .Where(t => t.InvoiceEmittedAt != null && t.InvoiceEmittedAt >= fromUtc && t.InvoiceEmittedAt < toUtc)
            .Select(t => new RelatorioFiscalDocumentoRow(
                t.Id,
                "Trabalho",
                t.Numero,
                t.InvoiceNumber,
                t.InvoiceEmittedAt!.Value,
                t.Cliente != null ? t.Cliente.Nome : null,
                t.PrecoFinalCents ?? t.OrcamentoCents ?? 0))
            .ToListAsync(ct);

        return reparacoes.Concat(trabalhos)
            .OrderBy(x => x.InvoiceEmittedAt)
            .ThenBy(x => x.Tipo)
            .ThenBy(x => x.NumeroInterno)
            .ToList();
    }
}
