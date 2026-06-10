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

    public async Task<IReadOnlyList<TopArtigoRow>> GetTopArtigosAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? Guid.Empty;

        // Linhas das vendas PAGAS no período (mesma janela da ReceitaVendas do snapshot).
        var linhas = await _db.Vendas
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId
                && v.Status == VendaStatus.Paga
                && v.Data >= fromUtc && v.Data < toUtc)
            .SelectMany(v => v.Items)
            .Select(i => new
            {
                i.Descricao,
                i.Quantidade,
                LinhaCents = i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents,
                CustoUnit = i.Part != null ? (int?)i.Part.CustoUnitarioCents : null,
            })
            .ToListAsync(ct);

        // Agregação em memória por descrição (case-insensitive) — escala de loja, não de marketplace.
        return linhas
            .GroupBy(l => l.Descricao.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var receita = g.Sum(x => (long)Math.Max(0, x.LinhaCents));
                // Margem só quando TODAS as linhas têm custo registado — meia-margem engana mais
                // do que "—" (o UI mostra "sem custo" quando null).
                long? margem = g.All(x => x.CustoUnit != null)
                    ? receita - g.Sum(x => (long)x.Quantidade * x.CustoUnit!.Value)
                    : null;
                return new TopArtigoRow(g.First().Descricao.Trim(), g.Sum(x => x.Quantidade), receita, margem);
            })
            .OrderByDescending(r => r.ReceitaCents)
            .Take(top)
            .ToList();
    }

    public async Task<IReadOnlyList<TopClienteReceitaRow>> GetTopClientesAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? Guid.Empty;

        var reparacoes = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                && r.EstadoPagamento == PaymentStatus.Pago
                && r.Cliente != null)
            .Select(r => new { r.ClienteId, Nome = r.Cliente!.Nome, Receita = (long)(r.PrecoFinalCents ?? r.OrcamentoCents ?? 0) })
            .ToListAsync(ct);

        var trabalhos = await _db.Trabalhos
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.Status == TrabalhoStatus.Concluido
                && t.DataConclusao != null && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc
                && t.EstadoPagamento == PaymentStatus.Pago
                && t.ClienteId != null && t.Cliente != null)
            .Select(t => new { ClienteId = t.ClienteId!.Value, Nome = t.Cliente!.Nome, Receita = (long)(t.PrecoFinalCents ?? t.OrcamentoCents ?? 0) })
            .ToListAsync(ct);

        var vendas = await _db.Vendas
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId
                && v.Status == VendaStatus.Paga
                && v.Data >= fromUtc && v.Data < toUtc
                && v.ClienteId != null && v.Cliente != null)
            .Select(v => new
            {
                ClienteId = v.ClienteId!.Value,
                Nome = v.Cliente!.Nome,
                Receita = (long)v.Items.Sum(i => i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents),
            })
            .ToListAsync(ct);

        var porCliente = new Dictionary<Guid, (string Nome, long Receita, int Docs)>();
        void Add(Guid id, string nome, long receita)
        {
            if (!porCliente.TryGetValue(id, out var atual)) atual = (nome, 0L, 0);
            porCliente[id] = (atual.Nome, atual.Receita + Math.Max(0, receita), atual.Docs + 1);
        }
        foreach (var r in reparacoes) Add(r.ClienteId, r.Nome, r.Receita);
        foreach (var t in trabalhos) Add(t.ClienteId, t.Nome, t.Receita);
        foreach (var v in vendas) Add(v.ClienteId, v.Nome, v.Receita);

        return porCliente
            .Select(kv => new TopClienteReceitaRow(kv.Key, kv.Value.Nome, kv.Value.Receita, kv.Value.Docs))
            .OrderByDescending(c => c.ReceitaCents)
            .Take(top)
            .ToList();
    }
}
