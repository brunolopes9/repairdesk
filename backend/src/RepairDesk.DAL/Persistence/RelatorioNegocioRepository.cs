using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public sealed class RelatorioNegocioRepository : IRelatorioNegocioRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public RelatorioNegocioRepository(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<RelatorioNegocioSnapshot> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? Guid.Empty;

        var reparacoesPagas = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.EntregueEm != null
                && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                && r.EstadoPagamento == PaymentStatus.Pago)
            .Select(r => new
            {
                r.Id,
                r.Numero,
                r.Equipamento,
                ClienteNome = r.Cliente != null ? r.Cliente.Nome : null,
                ReceitaCents = r.PrecoFinalCents ?? r.OrcamentoCents ?? 0,
            })
            .ToListAsync(ct);

        var trabalhosPagos = await _db.Trabalhos
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.Status == TrabalhoStatus.Concluido
                && t.DataConclusao != null
                && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc
                && t.EstadoPagamento == PaymentStatus.Pago)
            .Select(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0)
            .ToListAsync(ct);

        var vendasPagas = await _db.Vendas
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId
                && v.Status == VendaStatus.Paga
                && v.Data >= fromUtc && v.Data < toUtc)
            .Select(v => v.TotalCents)
            .ToListAsync(ct);

        var custoPecasCents = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.Motivo == PartMovimentoMotivo.UsoEmReparacao
                && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc
                && m.Part != null)
            .SumAsync(m => (int?)((m.Quantidade < 0 ? -m.Quantidade : m.Quantidade) * m.Part!.CustoUnitarioCents), ct) ?? 0;

        var opexCents = await _db.Despesas
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId
                && d.Data >= fromUtc && d.Data < toUtc
                && !d.IsCogs
                && d.Categoria != DespesaCategoria.Pecas
                && d.Categoria != DespesaCategoria.Material)
            .SumAsync(d => (int?)d.ValorCents, ct) ?? 0;

        var reparacaoIds = reparacoesPagas.Select(r => r.Id).ToHashSet();
        var custosPorReparacao = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.ReparacaoId != null
                && reparacaoIds.Contains(m.ReparacaoId.Value)
                && m.Motivo == PartMovimentoMotivo.UsoEmReparacao
                && m.Part != null)
            .GroupBy(m => m.ReparacaoId!.Value)
            .Select(g => new
            {
                ReparacaoId = g.Key,
                CustoCents = g.Sum(m => (m.Quantidade < 0 ? -m.Quantidade : m.Quantidade) * (m.Part != null ? m.Part.CustoUnitarioCents : 0)),
            })
            .ToListAsync(ct);
        var custoMap = custosPorReparacao.ToDictionary(x => x.ReparacaoId, x => x.CustoCents);

        var topReparacoes = reparacoesPagas
            .Select(r =>
            {
                var custo = custoMap.GetValueOrDefault(r.Id);
                return new TopReparacaoLucrativaRow(
                    r.Id,
                    r.Numero,
                    r.Equipamento,
                    r.ClienteNome,
                    r.ReceitaCents,
                    custo,
                    r.ReceitaCents - custo);
            })
            .OrderByDescending(r => r.LucroCents)
            .ThenBy(r => r.Numero)
            .Take(5)
            .ToList();

        var topPecasRows = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.Motivo == PartMovimentoMotivo.UsoEmReparacao
                && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc
                && m.Part != null)
            .GroupBy(m => new { m.PartId, m.Part!.Nome, m.Part.Sku })
            .Select(g => new
            {
                g.Key.PartId,
                g.Key.Nome,
                g.Key.Sku,
                Quantidade = g.Sum(m => m.Quantidade < 0 ? -m.Quantidade : m.Quantidade),
            })
            .OrderByDescending(p => p.Quantidade)
            .ThenBy(p => p.Nome)
            .Take(5)
            .ToListAsync(ct);
        var topPecas = topPecasRows
            .Select(p => new TopPecaUsadaRow(p.PartId, p.Nome, p.Sku, p.Quantidade))
            .ToList();

        var topFornecedoresRows = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.Motivo == PartMovimentoMotivo.Entrada
                && m.CreatedAt >= fromUtc && m.CreatedAt < toUtc
                && m.Part != null)
            .GroupBy(m => string.IsNullOrWhiteSpace(m.Part!.Fornecedor) ? "Sem fornecedor" : m.Part.Fornecedor!)
            .Select(g => new
            {
                Nome = g.Key,
                TotalCompradoCents = g.Sum(m => m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)),
            })
            .OrderByDescending(f => f.TotalCompradoCents)
            .ThenBy(f => f.Nome)
            .Take(5)
            .ToListAsync(ct);
        var topFornecedores = topFornecedoresRows
            .Select(f => new TopFornecedorComprasRow(f.Nome, f.TotalCompradoCents))
            .ToList();

        return new RelatorioNegocioSnapshot(
            ReceitaReparacoesCents: reparacoesPagas.Sum(r => r.ReceitaCents),
            ReceitaTrabalhosCents: trabalhosPagos.Sum(),
            ReceitaVendasCents: vendasPagas.Sum(),
            ReparacoesPagasCount: reparacoesPagas.Count,
            CustoPecasCents: custoPecasCents,
            OpexCents: opexCents,
            TopReparacoesLucrativas: topReparacoes,
            TopPecasUsadas: topPecas,
            TopFornecedores: topFornecedores);
    }

    public async Task<IReadOnlyList<FornecedorDefeitoRow>> GetTaxaDefeitoFornecedorAsync(
        DateTime fromUtc,
        CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? Guid.Empty;

        // Carrega vendas com IMEI + fornecedor + data, e em paralelo todos os IMEIs reparados
        // dentro do tenant. Cruzar em memória é mais simples que correlated subquery em EF e
        // o volume é manejável (vendas com IMEI são caras, dezenas/centenas por ano).
        var vendaItems = await _db.VendaItems
            .AsNoTracking()
            .Where(vi => vi.TenantId == tenantId
                && vi.FornecedorNome != null
                && vi.Imei != null
                && vi.Venda!.Data >= fromUtc)
            .Select(vi => new
            {
                vi.Imei,
                Fornecedor = vi.FornecedorNome!,
                DataVenda = vi.Venda!.Data,
            })
            .ToListAsync(ct);

        if (vendaItems.Count == 0) return Array.Empty<FornecedorDefeitoRow>();

        var imeisVendidos = vendaItems.Select(v => v.Imei!).Distinct().ToList();

        // Reparações com IMEI matching, agrupado para minimizar payload: para cada IMEI a data
        // mais antiga de criação. Se a reparação foi anterior à venda, não conta.
        var reparacoesPorImei = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Imei != null && imeisVendidos.Contains(r.Imei))
            .GroupBy(r => r.Imei!)
            .Select(g => new { Imei = g.Key, MinCreatedAt = g.Min(r => r.CreatedAt) })
            .ToListAsync(ct);

        var minCreatedByImei = reparacoesPorImei.ToDictionary(x => x.Imei, x => x.MinCreatedAt);

        return vendaItems
            .GroupBy(vi => vi.Fornecedor)
            .Select(g =>
            {
                var vendidos = g.Count();
                var comReparacao = g.Count(vi =>
                    minCreatedByImei.TryGetValue(vi.Imei!, out var minCreated) && minCreated > vi.DataVenda);
                var taxa = vendidos == 0
                    ? 0m
                    : Math.Round(comReparacao * 100m / vendidos, 2, MidpointRounding.AwayFromZero);
                return new FornecedorDefeitoRow(g.Key, vendidos, comReparacao, taxa);
            })
            .OrderByDescending(r => r.TaxaDefeitoPct)
            .ThenByDescending(r => r.ItemsVendidos)
            .ToList();
    }
}
