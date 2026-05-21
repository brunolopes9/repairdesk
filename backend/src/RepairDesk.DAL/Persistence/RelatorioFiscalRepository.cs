using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public sealed class RelatorioFiscalRepository : IRelatorioFiscalRepository
{
    private readonly AppDbContext _db;

    public RelatorioFiscalRepository(AppDbContext db) => _db = db;

    public async Task ClearInvoiceFieldsAsync(string tipo, Guid entityId, CancellationToken ct = default)
    {
        switch (tipo)
        {
            case "Reparacao":
                var r = await _db.Reparacoes.FirstOrDefaultAsync(x => x.Id == entityId, ct);
                if (r is not null)
                {
                    r.InvoiceProvider = BillingProvider.None;
                    r.InvoiceExternalId = null;
                    r.InvoiceNumber = null;
                    r.InvoicePdfUrl = null;
                    r.InvoiceEmittedAt = null;
                }
                break;
            case "Trabalho":
                var t = await _db.Trabalhos.FirstOrDefaultAsync(x => x.Id == entityId, ct);
                if (t is not null)
                {
                    t.InvoiceProvider = BillingProvider.None;
                    t.InvoiceExternalId = null;
                    t.InvoiceNumber = null;
                    t.InvoicePdfUrl = null;
                    t.InvoiceEmittedAt = null;
                }
                break;
            case "Venda":
                var v = await _db.Vendas.FirstOrDefaultAsync(x => x.Id == entityId, ct);
                if (v is not null)
                {
                    v.InvoiceProvider = BillingProvider.None;
                    v.InvoiceExternalId = null;
                    v.InvoiceNumber = null;
                    v.InvoicePdfUrl = null;
                    v.InvoiceEmittedAt = null;
                }
                break;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sprint 159: soma o custo com IVA das peças do stock consumidas em reparações pagas
    /// no período. O IVA dedutível desta soma calcula-se externamente (cents × 23/123).
    /// </summary>
    public async Task<int> SumPecasCustoComIvaAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Reparações pagas entregues no período.
        var reparacoesPagasIds = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (reparacoesPagasIds.Count == 0) return 0;

        // PartMovimentos.Quantidade é negativa para UsoEmReparacao, positiva para Devolucao.
        // -Sum(qty) dá o consumo líquido. CustoUnitarioCents inclui IVA da compra.
        var custo = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.ReparacaoId != null && reparacoesPagasIds.Contains(m.ReparacaoId.Value))
            .GroupBy(m => 1)
            .Select(g => g.Sum(m => -m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)))
            .FirstOrDefaultAsync(ct);
        return custo;
    }

    /// <summary>
    /// Sprint 159: soma o valor com IVA das Despesas imputadas a reparações/trabalhos pagos
    /// no período + despesas overhead (sem trabalho/reparação associados — renda, internet, etc).
    /// </summary>
    public async Task<int> SumDespesasComIvaAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var reparacoesPagasIds = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => r.Id)
            .ToListAsync(ct);
        var trabalhosPagosIds = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                && t.DataConclusao != null && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc
                && (t.EstadoPagamento == PaymentStatus.Pago || t.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(t => t.Id)
            .ToListAsync(ct);

        return await _db.Despesas
            .Where(d => d.Data >= fromUtc && d.Data < toUtc
                && ((d.ReparacaoId != null && reparacoesPagasIds.Contains(d.ReparacaoId.Value))
                 || (d.TrabalhoId != null && trabalhosPagosIds.Contains(d.TrabalhoId.Value))
                 || (d.ReparacaoId == null && d.TrabalhoId == null)))  // overhead
            .SumAsync(d => d.ValorCents, ct);
    }

    public async Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var reparacoes = await _db.Reparacoes
            .Where(r => r.InvoiceEmittedAt != null && r.InvoiceEmittedAt >= fromUtc && r.InvoiceEmittedAt < toUtc)
            .Select(r => new RelatorioFiscalDocumentoRow(
                r.Id,
                "Reparacao",
                r.Numero,
                r.InvoiceNumber,
                r.InvoiceExternalId,
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
                t.InvoiceExternalId,
                t.InvoiceEmittedAt!.Value,
                t.Cliente != null ? t.Cliente.Nome : null,
                t.PrecoFinalCents ?? t.OrcamentoCents ?? 0))
            .ToListAsync(ct);

        // Sprint 45: incluir Vendas/POS no relatorio fiscal.
        var vendas = await _db.Vendas
            .Where(v => v.InvoiceEmittedAt != null && v.InvoiceEmittedAt >= fromUtc && v.InvoiceEmittedAt < toUtc)
            .Select(v => new RelatorioFiscalDocumentoRow(
                v.Id,
                "Venda",
                v.Numero,
                v.InvoiceNumber,
                v.InvoiceExternalId,
                v.InvoiceEmittedAt!.Value,
                v.Cliente != null ? v.Cliente.Nome : null,
                v.Items.Sum(i => i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents)))
            .ToListAsync(ct);

        return reparacoes.Concat(trabalhos).Concat(vendas)
            .OrderBy(x => x.InvoiceEmittedAt)
            .ThenBy(x => x.Tipo)
            .ThenBy(x => x.NumeroInterno)
            .ToList();
    }
}
