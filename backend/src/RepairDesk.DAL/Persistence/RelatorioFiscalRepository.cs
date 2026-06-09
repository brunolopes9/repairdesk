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

    public async Task MarcarReciboEmitidoAsync(string invoiceExternalId, string reciboNumero, DateTime emitidoEm, CancellationToken ct = default)
    {
        // Sprint 528: a fatura mora numa de 3 tabelas (Reparacao/Trabalho/Venda). Procura por
        // InvoiceExternalId e marca o recibo. Se for um doc só-Moloni (sem entity local), no-op silencioso.
        var r = await _db.Reparacoes.FirstOrDefaultAsync(x => x.InvoiceExternalId == invoiceExternalId, ct);
        if (r is not null) { r.ReciboNumero = reciboNumero; r.ReciboEmitidoEm = emitidoEm; await _db.SaveChangesAsync(ct); return; }
        var t = await _db.Trabalhos.FirstOrDefaultAsync(x => x.InvoiceExternalId == invoiceExternalId, ct);
        if (t is not null) { t.ReciboNumero = reciboNumero; t.ReciboEmitidoEm = emitidoEm; await _db.SaveChangesAsync(ct); return; }
        var v = await _db.Vendas.FirstOrDefaultAsync(x => x.InvoiceExternalId == invoiceExternalId, ct);
        if (v is not null) { v.ReciboNumero = reciboNumero; v.ReciboEmitidoEm = emitidoEm; await _db.SaveChangesAsync(ct); }
    }

    /// <summary>
    /// Sprint 176+178b: soma o custo com IVA de TODAS as compras de stock (peças/material)
    /// no período. Inclui:
    /// - PartMovimentos Entrada (compras que entraram em stock + foram registadas como Parts)
    /// - Despesas Categoria=Pecas|Material (compras importadas como Despesa, sem PartMovimento)
    /// Fiscalmente correcto em PT — IVA é dedutível na compra, não no consumo.
    /// </summary>
    public async Task<int> SumPecasCustoComIvaAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var partsEntrada = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.Quantidade > 0
                && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc
                && m.Motivo == Core.Enums.PartMovimentoMotivo.Entrada
                && !m.ReverseCharge) // Sprint 525: intra-UE (autoliquidação) não é dedutível em PT
            .GroupBy(m => 1)
            .Select(g => g.Sum(m => m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)))
            .FirstOrDefaultAsync(ct);

        // Despesas categorizadas como Peças/Material são também compras de inventário
        // (ChatGPT validou: fiscalmente não são "despesas operacionais").
        var despesasPecas = await _db.Despesas
            .Where(d => d.Data >= fromUtc && d.Data < toUtc
                && !d.IsCogs
                && !d.ReverseCharge // Sprint 525: intra-UE (autoliquidação) não é dedutível em PT
                && (d.Categoria == Core.Enums.DespesaCategoria.Pecas
                 || d.Categoria == Core.Enums.DespesaCategoria.Material))
            .SumAsync(d => (int?)d.ValorCents, ct) ?? 0;

        return partsEntrada + despesasPecas;
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

        // Sprint 178b: exclui categorias Peças+Material — essas vão em SumPecasCustoComIvaAsync
        // ("Compras de stock"). Aqui ficam só despesas operacionais reais: Ferramenta, Software,
        // Transporte, Comunicações, Marketing, Serviços, Outro.
        return await _db.Despesas
            .Where(d => d.Data >= fromUtc && d.Data < toUtc
                && !d.IsCogs
                && !d.ReverseCharge // Sprint 525: intra-UE/serviços estrangeiros (autoliquidação) não dedutível em PT
                && d.Categoria != Core.Enums.DespesaCategoria.Pecas
                && d.Categoria != Core.Enums.DespesaCategoria.Material
                && ((d.ReparacaoId != null && reparacoesPagasIds.Contains(d.ReparacaoId.Value))
                 || (d.TrabalhoId != null && trabalhosPagosIds.Contains(d.TrabalhoId.Value))
                 || (d.ReparacaoId == null && d.TrabalhoId == null)))  // overhead
            .SumAsync(d => d.ValorCents, ct);
    }

    /// <summary>
    /// Sprint 180: detalhe das compras stock — PartMovimento Entrada + Despesas Peças/Material.
    /// Cada linha já com IVA extraído (× 23/123). Util para drill-down UI.
    /// </summary>
    public async Task<IReadOnlyList<IvaDeducaoLinha>> ListComprasStockAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var partsEntrada = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.Quantidade > 0
                && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc
                && m.Motivo == Core.Enums.PartMovimentoMotivo.Entrada
                && !m.ReverseCharge // Sprint 525: detalhe bate com o total (autoliquidação fora)
                && m.Part != null)
            .Select(m => new
            {
                m.CreatedAt,
                Descricao = m.Part!.Nome,
                m.Quantidade,
                CustoUnit = m.Part.CustoUnitarioCents,
            })
            .ToListAsync(ct);

        var despesasPecas = await _db.Despesas
            .AsNoTracking()
            .Where(d => d.Data >= fromUtc && d.Data < toUtc
                && !d.IsCogs
                && !d.ReverseCharge // Sprint 525: detalhe bate com o total (autoliquidação fora)
                && (d.Categoria == Core.Enums.DespesaCategoria.Pecas
                 || d.Categoria == Core.Enums.DespesaCategoria.Material))
            .Select(d => new { d.Data, d.Descricao, d.Fornecedor, d.ValorCents })
            .ToListAsync(ct);

        var result = new List<IvaDeducaoLinha>(partsEntrada.Count + despesasPecas.Count);
        foreach (var p in partsEntrada)
        {
            var valor = p.Quantidade * p.CustoUnit;
            var iva = (int)Math.Round(valor * 23.0 / 123.0);
            result.Add(new IvaDeducaoLinha(p.CreatedAt, $"{p.Descricao} ({p.Quantidade}× a {p.CustoUnit / 100m:0.00}€)", null, "stock-entrada", valor, iva));
        }
        foreach (var d in despesasPecas)
        {
            var iva = (int)Math.Round(d.ValorCents * 23.0 / 123.0);
            result.Add(new IvaDeducaoLinha(d.Data, d.Descricao, d.Fornecedor, "despesa-pecas", d.ValorCents, iva));
        }
        return result.OrderByDescending(l => l.Data).ToList();
    }

    /// <summary>
    /// Sprint 180: detalhe das despesas operacionais (não-Peças/Material, não-IsCogs) para drill-down.
    /// </summary>
    public async Task<IReadOnlyList<IvaDeducaoLinha>> ListDespesasOpExAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
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

        var despesas = await _db.Despesas
            .AsNoTracking()
            .Where(d => d.Data >= fromUtc && d.Data < toUtc
                && !d.IsCogs
                && d.Categoria != Core.Enums.DespesaCategoria.Pecas
                && d.Categoria != Core.Enums.DespesaCategoria.Material
                && ((d.ReparacaoId != null && reparacoesPagasIds.Contains(d.ReparacaoId.Value))
                 || (d.TrabalhoId != null && trabalhosPagosIds.Contains(d.TrabalhoId.Value))
                 || (d.ReparacaoId == null && d.TrabalhoId == null)))
            .Select(d => new { d.Data, d.Descricao, d.Fornecedor, d.ValorCents })
            .OrderByDescending(d => d.Data)
            .ToListAsync(ct);

        return despesas.Select(d => new IvaDeducaoLinha(
            d.Data, d.Descricao, d.Fornecedor, "despesa-opex", d.ValorCents,
            (int)Math.Round(d.ValorCents * 23.0 / 123.0))).ToList();
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

    public async Task<IReadOnlyList<DocumentoVendaRow>> ListVendaDocumentosDetalheAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var reparacoes = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.InvoiceEmittedAt != null && r.InvoiceEmittedAt >= fromUtc && r.InvoiceEmittedAt < toUtc)
            .Select(r => new DocumentoVendaRow(
                r.Id, "Reparacao", r.Numero,
                r.InvoiceNumber, r.InvoiceExternalId, r.InvoicePdfUrl, r.InvoiceProvider,
                r.InvoiceEmittedAt!.Value,
                r.ClienteId,
                r.Cliente != null ? r.Cliente.Nome : null,
                r.Cliente != null ? r.Cliente.Nif : null,
                r.PrecoFinalCents ?? r.OrcamentoCents ?? 0,
                r.ReciboNumero, r.ReciboEmitidoEm))
            .ToListAsync(ct);

        var trabalhos = await _db.Trabalhos
            .AsNoTracking()
            .Where(t => t.InvoiceEmittedAt != null && t.InvoiceEmittedAt >= fromUtc && t.InvoiceEmittedAt < toUtc)
            .Select(t => new DocumentoVendaRow(
                t.Id, "Trabalho", t.Numero,
                t.InvoiceNumber, t.InvoiceExternalId, t.InvoicePdfUrl, t.InvoiceProvider,
                t.InvoiceEmittedAt!.Value,
                t.ClienteId,
                t.Cliente != null ? t.Cliente.Nome : null,
                t.Cliente != null ? t.Cliente.Nif : null,
                t.PrecoFinalCents ?? t.OrcamentoCents ?? 0,
                t.ReciboNumero, t.ReciboEmitidoEm))
            .ToListAsync(ct);

        var vendas = await _db.Vendas
            .AsNoTracking()
            .Where(v => v.InvoiceEmittedAt != null && v.InvoiceEmittedAt >= fromUtc && v.InvoiceEmittedAt < toUtc)
            .Select(v => new DocumentoVendaRow(
                v.Id, "Venda", v.Numero,
                v.InvoiceNumber, v.InvoiceExternalId, v.InvoicePdfUrl, v.InvoiceProvider,
                v.InvoiceEmittedAt!.Value,
                v.ClienteId,
                v.Cliente != null ? v.Cliente.Nome : null,
                v.Cliente != null ? v.Cliente.Nif : null,
                v.Items.Sum(i => i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents),
                v.ReciboNumero, v.ReciboEmitidoEm))
            .ToListAsync(ct);

        // Sprint 529d: orçamentos (estimates) — família à parte, mas o Mender guarda o EstimateExternalId,
        // por isso listamo-los COM a origem certa (reparação/trabalho). TipoOverride="ORC" → não inflam
        // os totais de faturado e aparecem numa secção própria. Vendas (POS) não têm orçamento.
        var orcamentosRep = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.EstimateEmittedAt != null && r.EstimateEmittedAt >= fromUtc && r.EstimateEmittedAt < toUtc)
            .Select(r => new DocumentoVendaRow(
                r.Id, "Reparacao", r.Numero,
                r.EstimateNumber, r.EstimateExternalId, r.EstimatePdfUrl, r.InvoiceProvider,
                r.EstimateEmittedAt!.Value,
                r.ClienteId,
                r.Cliente != null ? r.Cliente.Nome : null,
                r.Cliente != null ? r.Cliente.Nif : null,
                r.OrcamentoCents ?? 0,
                null, null, "ORC"))
            .ToListAsync(ct);

        var orcamentosTrab = await _db.Trabalhos
            .AsNoTracking()
            .Where(t => t.EstimateEmittedAt != null && t.EstimateEmittedAt >= fromUtc && t.EstimateEmittedAt < toUtc)
            .Select(t => new DocumentoVendaRow(
                t.Id, "Trabalho", t.Numero,
                t.EstimateNumber, t.EstimateExternalId, t.EstimatePdfUrl, t.InvoiceProvider,
                t.EstimateEmittedAt!.Value,
                t.ClienteId,
                t.Cliente != null ? t.Cliente.Nome : null,
                t.Cliente != null ? t.Cliente.Nif : null,
                t.OrcamentoCents ?? 0,
                null, null, "ORC"))
            .ToListAsync(ct);

        return reparacoes.Concat(trabalhos).Concat(vendas).Concat(orcamentosRep).Concat(orcamentosTrab)
            .OrderByDescending(x => x.InvoiceEmittedAt)
            .ToList();
    }

    public async Task<MargemRegimeResult> SumMargemRegimeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Sprint 535: linhas de artigos em segunda mão (Recondicionado/Usado) de vendas FATURADAS no
        // período. Base da margem = Σ max(0, venda − custo); custo = Part.CustoUnitarioCents. Linhas
        // sem Part/custo são contadas à parte (não entram na base — não inflamos uma margem fictícia).
        var rows = await _db.Vendas
            .AsNoTracking()
            .Where(v => v.InvoiceEmittedAt != null && v.InvoiceEmittedAt >= fromUtc && v.InvoiceEmittedAt < toUtc)
            .SelectMany(v => v.Items)
            .Where(i => i.Condicao == CondicaoArtigo.Recondicionado || i.Condicao == CondicaoArtigo.Usado)
            .Select(i => new
            {
                i.Quantidade,
                i.PrecoUnitarioCents,
                i.DescontoCents,
                CustoUnit = i.Part != null ? (int?)i.Part.CustoUnitarioCents : null,
            })
            .ToListAsync(ct);

        var margemCents = 0;
        var semCusto = 0;
        foreach (var i in rows)
        {
            var vendaCents = Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents);
            if (i.CustoUnit is null or 0)
            {
                semCusto++;
                continue;
            }
            var custoCents = i.CustoUnit.Value * i.Quantidade;
            var m = vendaCents - custoCents;
            if (m > 0) margemCents += m; // margem negativa (vendido abaixo do custo) → 0 (regime per-item, conservador)
        }
        return new MargemRegimeResult(margemCents, semCusto);
    }
}
