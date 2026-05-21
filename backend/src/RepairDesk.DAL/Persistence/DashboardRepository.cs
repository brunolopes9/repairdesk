using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;
    public DashboardRepository(AppDbContext db) => _db = db;

    public async Task<DashboardSnapshot> GetSnapshotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var openRepairStatuses = new[]
        {
            RepairStatus.Recebido, RepairStatus.Diagnostico, RepairStatus.AguardaPeca,
            RepairStatus.EmReparacao, RepairStatus.Pronto
        };
        var openTrabalhoStatuses = new[]
        {
            TrabalhoStatus.Orcamento, TrabalhoStatus.Aceite, TrabalhoStatus.EmExecucao
        };

        // Receita do mês: Reparações entregues+pagas no intervalo + Trabalhos concluídos+pagos no intervalo
        var reparacoesPagas = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                        && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => new { r.PrecoFinalCents, r.OrcamentoCents })
            .ToListAsync(ct);
        var trabalhosPagos = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                        && t.DataConclusao != null && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc
                        && (t.EstadoPagamento == PaymentStatus.Pago || t.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(t => new { t.PrecoFinalCents, t.OrcamentoCents, t.Categoria })
            .ToListAsync(ct);
        var vendasPagas = await _db.Vendas
            .Where(v => v.Status == VendaStatus.Paga && v.Data >= fromUtc && v.Data < toUtc)
            .Select(v => new { v.Id, v.ClienteId, ClienteNome = v.Cliente != null ? v.Cliente.Nome : null, v.TotalCents, v.Data })
            .ToListAsync(ct);

        var receitaCents = reparacoesPagas.Sum(r => r.PrecoFinalCents ?? r.OrcamentoCents ?? 0)
                         + trabalhosPagos.Sum(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0)
                         + vendasPagas.Sum(v => v.TotalCents);
        var hoje = DateTime.UtcNow.Date;
        var vendasHojeCents = vendasPagas
            .Where(v => v.Data >= hoje && v.Data < hoje.AddDays(1))
            .Sum(v => v.TotalCents);
        var vendasMesCents = vendasPagas.Sum(v => v.TotalCents);

        // Despesas do mês
        var despesas = await _db.Despesas
            .Where(d => d.Data >= fromUtc && d.Data < toUtc)
            .Select(d => new { d.Categoria, d.ValorCents })
            .ToListAsync(ct);
        var despesasCents = despesas.Sum(d => d.ValorCents);

        // Counters
        var reparacoesAbertas = await _db.Reparacoes.CountAsync(r => openRepairStatuses.Contains(r.Estado), ct);
        var trabalhosAbertos = await _db.Trabalhos.CountAsync(t => openTrabalhoStatuses.Contains(t.Status), ct);
        var reparacoesEntreguesMes = await _db.Reparacoes
            .CountAsync(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc, ct);
        var trabalhosConcluidosMes = await _db.Trabalhos
            .CountAsync(t => t.Status == TrabalhoStatus.Concluido
                          && t.DataConclusao != null && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc, ct);

        // Receita por categoria (Reparacoes contam todas como "Reparacao")
        var receitaPorCategoria = new List<CategoriaTotal>();
        if (reparacoesPagas.Count > 0)
        {
            receitaPorCategoria.Add(new CategoriaTotal(
                "Reparação",
                reparacoesPagas.Count,
                reparacoesPagas.Sum(r => r.PrecoFinalCents ?? r.OrcamentoCents ?? 0)));
        }
        receitaPorCategoria.AddRange(trabalhosPagos
            .GroupBy(t => t.Categoria)
            .Select(g => new CategoriaTotal(
                LabelFor(g.Key),
                g.Count(),
                g.Sum(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0))));
        if (vendasPagas.Count > 0)
        {
            receitaPorCategoria.Add(new CategoriaTotal("Vendas", vendasPagas.Count, vendasPagas.Sum(v => v.TotalCents)));
        }

        // Despesa por categoria
        var despesaPorCategoria = despesas
            .GroupBy(d => d.Categoria)
            .Select(g => new CategoriaTotal(
                LabelForDespesa(g.Key),
                g.Count(),
                g.Sum(x => x.ValorCents)))
            .OrderByDescending(c => c.TotalCents)
            .ToList();

        // Top clientes (últimos 90 dias por receita)
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var clientesReparacoes = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= ninetyDaysAgo
                     && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial)
                     && r.Cliente != null)
            .Select(r => new { r.ClienteId, Nome = r.Cliente!.Nome, Cents = r.PrecoFinalCents ?? r.OrcamentoCents ?? 0 })
            .ToListAsync(ct);
        var clientesTrabalhos = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido && t.DataConclusao >= ninetyDaysAgo
                     && (t.EstadoPagamento == PaymentStatus.Pago || t.EstadoPagamento == PaymentStatus.PagoParcial)
                     && t.ClienteId != null && t.Cliente != null)
            .Select(t => new { ClienteId = t.ClienteId!.Value, Nome = t.Cliente!.Nome, Cents = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0 })
            .ToListAsync(ct);
        var clientesVendas = await _db.Vendas
            .Where(v => v.Status == VendaStatus.Paga && v.Data >= ninetyDaysAgo
                     && v.ClienteId != null && v.Cliente != null)
            .Select(v => new { ClienteId = v.ClienteId!.Value, Nome = v.Cliente!.Nome, Cents = v.TotalCents })
            .ToListAsync(ct);

        var topClientes = clientesReparacoes.Cast<dynamic>().Concat(clientesTrabalhos.Cast<dynamic>()).Concat(clientesVendas.Cast<dynamic>())
            .GroupBy(x => (Guid)x.ClienteId)
            .Select(g => new TopClienteRow(
                g.Key,
                (string)g.First().Nome,
                g.Sum(x => (int)x.Cents),
                g.Count()))
            .OrderByDescending(c => c.TotalCents)
            .Take(5)
            .ToList();
        var vendaItemsPagos = await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Venda != null && i.Venda.Status == VendaStatus.Paga && i.Venda.Data >= fromUtc && i.Venda.Data < toUtc)
            .Select(i => new { i.PartId, i.Descricao, i.Quantidade, i.PrecoUnitarioCents, i.DescontoCents })
            .ToListAsync(ct);
        var topProdutos = vendaItemsPagos
            .GroupBy(i => new { i.PartId, i.Descricao })
            .Select(g => new TopProdutoVendidoRow(
                g.Key.PartId,
                g.Key.Descricao,
                g.Sum(x => x.Quantidade),
                g.Sum(x => Math.Max(0, x.Quantidade * x.PrecoUnitarioCents - x.DescontoCents))))
            .OrderByDescending(x => x.TotalCents)
            .Take(5)
            .ToList();

        return new DashboardSnapshot(
            ReceitaCentsMes: receitaCents,
            DespesasCentsMes: despesasCents,
            VendasHojeCents: vendasHojeCents,
            VendasMesCents: vendasMesCents,
            ReparacoesAbertas: reparacoesAbertas,
            TrabalhosAbertos: trabalhosAbertos,
            ReparacoesEntreguesMes: reparacoesEntreguesMes,
            TrabalhosConcluidosMes: trabalhosConcluidosMes,
            ReceitaPorCategoria: receitaPorCategoria.OrderByDescending(c => c.TotalCents).ToList(),
            DespesaPorCategoria: despesaPorCategoria,
            TopClientes: topClientes,
            TopProdutosVendidos: topProdutos);
    }

    public async Task<FinanceiroSnapshot> GetFinanceiroAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Reparações pagas no intervalo
        var reparacoesPagas = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                        && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => new { r.Id, r.PrecoFinalCents, r.OrcamentoCents })
            .ToListAsync(ct);

        var trabalhosPagos = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                        && t.DataConclusao != null && t.DataConclusao >= fromUtc && t.DataConclusao < toUtc
                        && (t.EstadoPagamento == PaymentStatus.Pago || t.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(t => new { t.Id, t.PrecoFinalCents, t.OrcamentoCents, t.Categoria })
            .ToListAsync(ct);

        // Vendas pagas no intervalo + custo imputado das peças vendidas (COGS)
        var vendasPagasItems = await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Venda != null
                        && i.Venda.Status == VendaStatus.Paga
                        && i.Venda.Data >= fromUtc && i.Venda.Data < toUtc)
            .Select(i => new
            {
                Receita = Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents),
                Custo = i.Part != null ? i.Part.CustoUnitarioCents * i.Quantidade : 0,
            })
            .ToListAsync(ct);
        var receitaVendas = vendasPagasItems.Sum(x => x.Receita);
        var custoVendas = vendasPagasItems.Sum(x => x.Custo);
        var countVendas = await _db.Vendas
            .CountAsync(v => v.Status == VendaStatus.Paga && v.Data >= fromUtc && v.Data < toUtc, ct);

        var reparacoesPagasIds = reparacoesPagas.Select(r => r.Id).ToHashSet();
        var trabalhosPagosIds = trabalhosPagos.Select(t => t.Id).ToHashSet();

        // Pendentes (concluídos mas não pagos)
        var reparacoesPendentes = await _db.Reparacoes
            .Where(r => r.Estado == RepairStatus.Entregue
                        && r.EstadoPagamento == PaymentStatus.NaoPago)
            .Select(r => new { r.PrecoFinalCents, r.OrcamentoCents })
            .ToListAsync(ct);
        var trabalhosPendentes = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                        && t.EstadoPagamento == PaymentStatus.NaoPago)
            .Select(t => new { t.PrecoFinalCents, t.OrcamentoCents })
            .ToListAsync(ct);
        var receitaPendente = reparacoesPendentes.Sum(r => r.PrecoFinalCents ?? r.OrcamentoCents ?? 0)
                            + trabalhosPendentes.Sum(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0);

        // Despesas no intervalo
        var despesas = await _db.Despesas
            .Where(d => d.Data >= fromUtc && d.Data < toUtc)
            .Select(d => new { d.Categoria, d.ValorCents, d.ReparacaoId, d.TrabalhoId })
            .ToListAsync(ct);

        var custoImputadoTotal = 0;
        var investimentoStock = 0;
        var custoPorReparacao = new Dictionary<Guid, int>();
        var custoPorTrabalho = new Dictionary<Guid, int>();

        foreach (var d in despesas)
        {
            var imputadaPaga = (d.ReparacaoId is Guid rid && reparacoesPagasIds.Contains(rid))
                            || (d.TrabalhoId is Guid tid && trabalhosPagosIds.Contains(tid));

            if (imputadaPaga)
            {
                custoImputadoTotal += d.ValorCents;
                if (d.ReparacaoId is Guid rId)
                    custoPorReparacao[rId] = custoPorReparacao.GetValueOrDefault(rId) + d.ValorCents;
                if (d.TrabalhoId is Guid tId)
                    custoPorTrabalho[tId] = custoPorTrabalho.GetValueOrDefault(tId) + d.ValorCents;
            }
            else
            {
                // Não imputada a pago — stock / pendente / overhead
                investimentoStock += d.ValorCents;
            }
        }

        // Sprint 155: além de Despesas, somar peças do stock consumidas (PartMovimento líquido)
        // imputadas a reparações pagas. Custo = -Sum(quantidade) * Part.CustoUnitarioCents.
        // (Quantidade é negativa para UsoEmReparacao, positiva para Devolucao — net dá o consumo.)
        var custoPecasPorReparacao = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.ReparacaoId != null && reparacoesPagasIds.Contains(m.ReparacaoId.Value))
            .GroupBy(m => m.ReparacaoId!.Value)
            .Select(g => new
            {
                ReparacaoId = g.Key,
                CustoCents = g.Sum(m => -m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)),
            })
            .ToListAsync(ct);
        foreach (var row in custoPecasPorReparacao)
        {
            custoImputadoTotal += row.CustoCents;
            custoPorReparacao[row.ReparacaoId] = custoPorReparacao.GetValueOrDefault(row.ReparacaoId) + row.CustoCents;
        }

        var receitaReparacoes = reparacoesPagas.Sum(r => r.PrecoFinalCents ?? r.OrcamentoCents ?? 0);
        var custoReparacoes = custoPorReparacao.Values.Sum();
        var receitaTrabalhos = trabalhosPagos.Sum(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0);
        var receitaTotal = receitaReparacoes + receitaTrabalhos + receitaVendas;
        var custoTotalImputado = custoImputadoTotal + custoVendas;
        var lucroRealizado = receitaTotal - custoTotalImputado;

        var porCategoria = new List<CategoriaFinanceiraRow>();
        if (reparacoesPagas.Count > 0)
        {
            porCategoria.Add(new CategoriaFinanceiraRow(
                "Reparação",
                reparacoesPagas.Count,
                receitaReparacoes,
                custoReparacoes,
                receitaReparacoes - custoReparacoes));
        }
        foreach (var grupo in trabalhosPagos.GroupBy(t => t.Categoria))
        {
            var receita = grupo.Sum(t => t.PrecoFinalCents ?? t.OrcamentoCents ?? 0);
            var custo = grupo.Sum(t => custoPorTrabalho.GetValueOrDefault(t.Id));
            porCategoria.Add(new CategoriaFinanceiraRow(
                LabelFor(grupo.Key),
                grupo.Count(),
                receita,
                custo,
                receita - custo));
        }
        if (countVendas > 0)
        {
            porCategoria.Add(new CategoriaFinanceiraRow(
                "Vendas",
                countVendas,
                receitaVendas,
                custoVendas,
                receitaVendas - custoVendas));
        }
        porCategoria = porCategoria.OrderByDescending(c => c.ReceitaCents).ToList();

        return new FinanceiroSnapshot(
            ReceitaRealizadaCents: receitaTotal,
            CustoImputadoCents: custoTotalImputado,
            LucroRealizadoCents: lucroRealizado,
            ReceitaPendenteCents: receitaPendente,
            InvestimentoStockCents: investimentoStock,
            PorCategoria: porCategoria);
    }

    public async Task<IReadOnlyList<MesFinanceiroRow>> GetTendenciaAsync(int mesesAtras, CancellationToken ct = default)
    {
        if (mesesAtras < 1) mesesAtras = 1;
        if (mesesAtras > 24) mesesAtras = 24;

        var now = DateTime.UtcNow;
        var fim = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var inicio = fim.AddMonths(-mesesAtras);

        // Reparações pagas no intervalo
        var reparacoesPagas = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= inicio && r.EntregueEm < fim
                        && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => new { r.Id, Data = r.EntregueEm!.Value, Cents = r.PrecoFinalCents ?? r.OrcamentoCents ?? 0 })
            .ToListAsync(ct);

        var trabalhosPagos = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                        && t.DataConclusao != null && t.DataConclusao >= inicio && t.DataConclusao < fim
                        && (t.EstadoPagamento == PaymentStatus.Pago || t.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(t => new { t.Id, Data = t.DataConclusao!.Value, Cents = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0 })
            .ToListAsync(ct);

        var reparacoesPagasIds = reparacoesPagas.Select(r => r.Id).ToHashSet();
        var trabalhosPagosIds = trabalhosPagos.Select(t => t.Id).ToHashSet();

        var despesas = await _db.Despesas
            .Where(d => d.Data >= inicio && d.Data < fim
                        && ((d.ReparacaoId != null && reparacoesPagasIds.Contains(d.ReparacaoId.Value))
                         || (d.TrabalhoId != null && trabalhosPagosIds.Contains(d.TrabalhoId.Value))))
            .Select(d => new { Data = d.Data, Cents = d.ValorCents })
            .ToListAsync(ct);

        // Sprint 155: incluir custo das peças do stock (PartMovimento) nas reparações pagas.
        // Bucketing por mês usa a data EntregueEm da reparação (não a data do movimento).
        var entregueEmPorRep = reparacoesPagas.ToDictionary(r => r.Id, r => r.Data);
        var custoPecasReparacoes = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.ReparacaoId != null && reparacoesPagasIds.Contains(m.ReparacaoId.Value))
            .GroupBy(m => m.ReparacaoId!.Value)
            .Select(g => new { ReparacaoId = g.Key, Custo = g.Sum(m => -m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)) })
            .ToListAsync(ct);
        var custoPecasPorBucket = custoPecasReparacoes
            .Select(c => new { Data = entregueEmPorRep[c.ReparacaoId], c.Custo })
            .ToList();

        // Vendas pagas com COGS (custo da peça × quantidade) por mês
        var vendaItensPagos = await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Venda != null && i.Venda.Status == VendaStatus.Paga
                        && i.Venda.Data >= inicio && i.Venda.Data < fim)
            .Select(i => new
            {
                Data = i.Venda!.Data,
                Receita = Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents),
                Custo = i.Part != null ? i.Part.CustoUnitarioCents * i.Quantidade : 0,
            })
            .ToListAsync(ct);

        var result = new List<MesFinanceiroRow>();
        for (int i = 0; i < mesesAtras; i++)
        {
            var bucketStart = inicio.AddMonths(i);
            var bucketEnd = bucketStart.AddMonths(1);
            var receita = reparacoesPagas.Where(r => r.Data >= bucketStart && r.Data < bucketEnd).Sum(r => r.Cents)
                        + trabalhosPagos.Where(t => t.Data >= bucketStart && t.Data < bucketEnd).Sum(t => t.Cents)
                        + vendaItensPagos.Where(v => v.Data >= bucketStart && v.Data < bucketEnd).Sum(v => v.Receita);
            var custo = despesas.Where(d => d.Data >= bucketStart && d.Data < bucketEnd).Sum(d => d.Cents)
                      + custoPecasPorBucket.Where(c => c.Data >= bucketStart && c.Data < bucketEnd).Sum(c => c.Custo)
                      + vendaItensPagos.Where(v => v.Data >= bucketStart && v.Data < bucketEnd).Sum(v => v.Custo);
            result.Add(new MesFinanceiroRow(bucketStart.Year, bucketStart.Month, receita, custo, receita - custo));
        }
        return result;
    }

    public async Task<IReadOnlyList<ReparacaoTopRow>> GetTopReparacoesAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
    {
        if (limit < 1) limit = 5;
        if (limit > 20) limit = 20;

        var reparacoesPagas = await _db.Reparacoes
            .Where(r => r.EntregueEm != null && r.EntregueEm >= fromUtc && r.EntregueEm < toUtc
                        && (r.EstadoPagamento == PaymentStatus.Pago || r.EstadoPagamento == PaymentStatus.PagoParcial))
            .Select(r => new
            {
                r.Id,
                r.Numero,
                r.Equipamento,
                ClienteNome = r.Cliente != null ? r.Cliente.Nome : null,
                Receita = r.PrecoFinalCents ?? r.OrcamentoCents ?? 0,
            })
            .ToListAsync(ct);

        var ids = reparacoesPagas.Select(r => r.Id).ToHashSet();
        var custoDespesas = await _db.Despesas
            .Where(d => d.ReparacaoId != null && ids.Contains(d.ReparacaoId.Value))
            .GroupBy(d => d.ReparacaoId!.Value)
            .Select(g => new { Id = g.Key, Custo = g.Sum(x => x.ValorCents) })
            .ToListAsync(ct);
        // Sprint 155: incluir custo das peças do stock (PartMovimento líquido), como no GetFinanceiroAsync.
        var custoPecas = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.ReparacaoId != null && ids.Contains(m.ReparacaoId.Value))
            .GroupBy(m => m.ReparacaoId!.Value)
            .Select(g => new { Id = g.Key, Custo = g.Sum(m => -m.Quantidade * (m.Part != null ? m.Part.CustoUnitarioCents : 0)) })
            .ToListAsync(ct);
        var custoMap = new Dictionary<Guid, int>();
        foreach (var c in custoDespesas) custoMap[c.Id] = c.Custo;
        foreach (var c in custoPecas) custoMap[c.Id] = custoMap.GetValueOrDefault(c.Id) + c.Custo;

        return reparacoesPagas
            .Select(r => new ReparacaoTopRow(
                r.Id,
                r.Numero,
                r.Equipamento,
                r.ClienteNome,
                r.Receita,
                custoMap.GetValueOrDefault(r.Id),
                r.Receita - custoMap.GetValueOrDefault(r.Id)))
            .OrderByDescending(r => r.LucroCents)
            .Take(limit)
            .ToList();
    }

    public async Task<AlertasSnapshot> GetAlertasAsync(CancellationToken ct = default)
    {
        var trabalhosNaoPagos = await _db.Trabalhos
            .Where(t => t.Status == TrabalhoStatus.Concluido
                        && t.EstadoPagamento == PaymentStatus.NaoPago)
            .OrderByDescending(t => t.DataConclusao)
            .Select(t => new ItemPorCobrarRow(
                t.Id,
                t.Numero,
                t.Titulo,
                t.Cliente != null ? t.Cliente.Nome : null,
                t.PrecoFinalCents ?? t.OrcamentoCents ?? 0,
                t.DataConclusao))
            .Take(50)
            .ToListAsync(ct);

        var reparacoesNaoPagas = await _db.Reparacoes
            .Where(r => r.Estado == RepairStatus.Entregue
                        && r.EstadoPagamento == PaymentStatus.NaoPago)
            .OrderByDescending(r => r.EntregueEm)
            .Select(r => new ItemPorCobrarRow(
                r.Id,
                r.Numero,
                r.Equipamento,
                r.Cliente != null ? r.Cliente.Nome : null,
                r.PrecoFinalCents ?? r.OrcamentoCents ?? 0,
                r.EntregueEm))
            .Take(50)
            .ToListAsync(ct);

        var despesasOrfas = await _db.Despesas
            .Where(d => d.TrabalhoId == null && d.ReparacaoId == null)
            .OrderByDescending(d => d.Data)
            .Select(d => new DespesaOrfaRow(
                d.Id,
                d.Descricao,
                (int)d.Categoria,
                d.ValorCents,
                d.Data,
                d.Fornecedor))
            .Take(50)
            .ToListAsync(ct);

        return new AlertasSnapshot(
            trabalhosNaoPagos,
            reparacoesNaoPagas,
            despesasOrfas,
            trabalhosNaoPagos.Sum(x => x.ValorCents) + reparacoesNaoPagas.Sum(x => x.ValorCents),
            despesasOrfas.Sum(x => x.ValorCents));
    }

    public Task<int> GetReparacoesCountAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => _db.Reparacoes
            .CountAsync(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc, ct);

    public async Task<ReparacoesEmGarantiaSnapshot> GetReparacoesEmGarantiaAsync(
        DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
    {
        // Join Reparacoes.Imei ↔ VendaItems.Imei (mesmo tenant). Cliente trouxe um
        // equipamento vendido aqui antes — indicador de defeito pós-venda (Sprint 84).
        // NOTA: NÃO restringe à garantia activa — qualquer reparação de equipamento
        // vendido aqui é interessante para análise de qualidade, mesmo fora prazo.
        var rows = await (
            from r in _db.Reparacoes.AsNoTracking()
            join vi in _db.VendaItems.AsNoTracking() on r.Imei equals vi.Imei
            where r.Imei != null
                && r.CreatedAt >= fromUtc && r.CreatedAt < toUtc
                && vi.Venda != null
                && vi.Venda.Status == VendaStatus.Paga
                && vi.Venda.Data < r.CreatedAt  // venda tem de ser anterior à reparação
            orderby r.CreatedAt descending
            select new ReparacaoEmGarantiaRow(
                r.Id,
                r.Numero,
                r.CreatedAt,
                r.Equipamento,
                r.Imei!,
                r.Estado == RepairStatus.Entregue,
                r.OrcamentoCents,
                vi.Venda!.Id,
                vi.Venda.Numero,
                vi.Venda.Data,
                vi.Venda.Cliente != null ? vi.Venda.Cliente.Nome : null))
            .Take(limit)
            .ToListAsync(ct);

        var total = rows.Count;
        var valorOrcamento = rows.Sum(r => r.OrcamentoCents ?? 0);

        return new ReparacoesEmGarantiaSnapshot(total, valorOrcamento, rows);
    }

    private static string LabelFor(JobCategory c) => c switch
    {
        JobCategory.Reparacao => "Reparação",
        JobCategory.Website => "Website",
        JobCategory.Software => "Software",
        JobCategory.EquipamentoNovo => "Hardware",
        JobCategory.Servicos => "Serviços",
        _ => "Outro",
    };

    private static string LabelForDespesa(DespesaCategoria c) => c switch
    {
        DespesaCategoria.Pecas => "Peças",
        DespesaCategoria.Material => "Material",
        DespesaCategoria.Ferramenta => "Ferramenta",
        DespesaCategoria.Software => "Software",
        DespesaCategoria.Transporte => "Transporte",
        DespesaCategoria.Comunicacoes => "Comunicações",
        DespesaCategoria.Marketing => "Marketing",
        DespesaCategoria.Servicos => "Serviços",
        _ => "Outro",
    };
}
