using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Fiscal;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.Financas;

public sealed record FinancasLinhaDto(string Nome, decimal Valor);
public sealed record FinancasFixaDto(string Descricao, decimal ValorTipico, decimal RegistadoEsteMes, bool Ok);

public sealed record FinancasMesDto(
    int Ano, int Mes, decimal Objetivo,
    // negócios
    decimal LucroInformatica, decimal PayoutsTrading, decimal TotalLucros,
    // despesas
    decimal DespesasInformatica, decimal DespesasTrading, decimal DespesasGerais, decimal DespesasPessoais, decimal Obrigacoes,
    decimal TotalDespesas, decimal ImpostosPagos,
    decimal ResultadoInformatica, decimal ResultadoTrading,
    decimal Saldo, decimal FaltaParaObjetivo,
    // atividade
    int Vendas, decimal Faturado, decimal MargemMedia, decimal TicketMedio, decimal Gasto, decimal Investimento,
    IReadOnlyList<FinancasLinhaDto> MaioresDespesas,
    IReadOnlyList<FinancasFixaDto> Fixas,
    int VendasSemFatura);

public sealed record FinancasResumoMesDto(int Mes, decimal LucroInformatica, decimal PayoutsTrading, decimal DespesasNegocio,
    decimal DespesasPessoais, decimal Saldo, decimal Objetivo);

public interface IFinancasService
{
    Task<FinancasMesDto> MesAsync(int ano, int mes, CancellationToken ct = default);
    Task<IReadOnlyList<FinancasResumoMesDto>> AnoAsync(int ano, CancellationToken ct = default);
    Task DefinirObjetivoAsync(decimal objetivo, CancellationToken ct = default);
}

/// <summary>
/// Sprint 633 (Doc 99): o Excel «Gestão Financeira» do Bruno dentro do Mender — «quanto ganhei este mês, em que
/// negócio, onde gastei, e estou acima ou abaixo do objetivo?». Regras (SPEC_gestao_financeira §1):
/// <list type="bullet">
/// <item>Informática = vendas entregues da loja (com ou sem fatura): lucro do motor (cobrado − peças − IVA devido).</item>
/// <item>Trading = payouts (vendas da atividade Trading), sem custo.</item>
/// <item>Despesas do negócio por afetação (LopesTech → Informática, Trading, Mista → Gerais); Pessoais; Obrigações
/// (Segurança Social). Pagamentos de IVA/IRS são tesouraria: mostram-se mas não entram no saldo.</item>
/// <item>Saldo = lucros − despesas; comparado com o objetivo mensal (1 500 € por omissão).</item>
/// </list>
/// Camisolas: em espera até haver enquadramento fiscal (Doc 99).
/// </summary>
public sealed class FinancasService : IFinancasService
{
    public const decimal ObjetivoPadrao = 1500m;
    private const int Max = 10_000;

    private readonly IVendaRepository _vendas;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;

    public FinancasService(IVendaRepository vendas, IDespesaRepository despesas, ITenantRepository tenants, ITenantContext tenant)
    {
        _vendas = vendas;
        _despesas = despesas;
        _tenants = tenants;
        _tenant = tenant;
    }

    public async Task<FinancasMesDto> MesAsync(int ano, int mes, CancellationToken ct = default)
    {
        Validar(ano, mes);
        var objetivo = (await TenantAsync(ct)).ObjetivoMensal ?? ObjetivoPadrao;
        var de = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = de.AddMonths(1);

        var (vendas, _) = await _vendas.SearchAsync(new VendaFiltro(de, ate, Estado: VendaEstado.Entregue), 1, Max, ct);
        decimal lucroInf = 0, faturadoInf = 0, payouts = 0;
        int nInf = 0, semFatura = 0;
        foreach (var v in vendas)
        {
            if (v.Atividade == VendaAtividade.Trading)
            {
                payouts += v.Items.Sum(i => VendaService.Calcular(i, v.ForaApuramento).ValorCobradoSemIva);
                continue;
            }
            nInf++;
            if (v.InvoiceNumber is null) semFatura++;
            foreach (var i in v.Items)
            {
                var c = VendaService.Calcular(i, v.ForaApuramento);
                lucroInf += c.Lucro;
                faturadoInf += c.ValorCobradoSemIva;
            }
        }

        var (despesas, _) = await _despesas.SearchAsync(null, null, null, false, false, de, ate.AddTicks(-1), null, 1, Max, ct);
        decimal Valor(Despesa d) => d.ValorCents / 100m;
        bool Imposto(Despesa d) => d.Categoria is DespesaCategoria.PagamentoIva or DespesaCategoria.PagamentoIrs;
        var contam = despesas.Where(d => !d.IsCogs && !Imposto(d)).ToList();
        var negocio = contam.Where(d => d.Ambito == DespesaAmbito.Negocio).ToList();
        var despInf = negocio.Where(d => d.Afetacao == AfetacaoIva.LopesTech).Sum(Valor);
        var despTrad = negocio.Where(d => d.Afetacao == AfetacaoIva.Trading).Sum(Valor);
        var despGer = negocio.Where(d => d.Afetacao == AfetacaoIva.Mista).Sum(Valor);
        var pessoais = contam.Where(d => d.Ambito == DespesaAmbito.Pessoal).Sum(Valor);
        var obrig = contam.Where(d => d.Ambito == DespesaAmbito.Obrigacao).Sum(Valor);
        var totalDesp = despInf + despTrad + despGer + pessoais + obrig;
        var impostos = despesas.Where(Imposto).Sum(Valor);

        var totalLucros = lucroInf + payouts;
        var saldo = totalLucros - totalDesp;

        var maiores = contam.GroupBy(d => d.Categoria)
            .Select(g => new FinancasLinhaDto(NomeCategoria(g.Key), E(g.Sum(Valor))))
            .OrderByDescending(x => x.Valor).Take(6).ToList();

        var fixas = await FixasAsync(de, despesas, ct);

        return new FinancasMesDto(ano, mes, objetivo,
            E(lucroInf), E(payouts), E(totalLucros),
            E(despInf), E(despTrad), E(despGer), E(pessoais), E(obrig), E(totalDesp), E(impostos),
            E(lucroInf - despInf), E(payouts - despTrad),
            E(saldo), E(Math.Max(0, objetivo - saldo)),
            nInf, E(faturadoInf), faturadoInf > 0 ? Math.Round(lucroInf / faturadoInf, 4) : 0m,
            nInf > 0 ? E(faturadoInf / nInf) : 0m,
            E(contam.Where(d => d.Natureza == DespesaNatureza.Gasto).Sum(Valor)),
            E(contam.Where(d => d.Natureza == DespesaNatureza.Investimento).Sum(Valor)),
            maiores, fixas, semFatura);
    }

    public async Task<IReadOnlyList<FinancasResumoMesDto>> AnoAsync(int ano, CancellationToken ct = default)
    {
        Validar(ano, 1);
        var lista = new List<FinancasResumoMesDto>();
        var agora = DateTime.UtcNow;
        for (var m = 1; m <= 12; m++)
        {
            if (new DateTime(ano, m, 1) > agora) break;
            var r = await MesAsync(ano, m, ct);
            lista.Add(new FinancasResumoMesDto(m, r.LucroInformatica, r.PayoutsTrading,
                r.DespesasInformatica + r.DespesasTrading + r.DespesasGerais, r.DespesasPessoais + r.Obrigacoes, r.Saldo, r.Objetivo));
        }
        return lista;
    }

    public async Task DefinirObjetivoAsync(decimal objetivo, CancellationToken ct = default)
    {
        if (objetivo is < 0 or > 1_000_000) throw new ValidationException("objetivo_invalido", "Objetivo de 0 a 1 000 000 €.");
        var t = await TenantAsync(ct);
        t.ObjetivoMensal = objetivo;
        await _tenants.SaveAsync(ct);
    }

    /// <summary>
    /// «Fixas» do Excel: as despesas marcadas como recorrentes nos últimos 4 meses (por fornecedor ou descrição);
    /// estão OK se já há uma deste mês.
    /// </summary>
    private async Task<IReadOnlyList<FinancasFixaDto>> FixasAsync(DateTime inicioMes, IReadOnlyList<Despesa> doMes, CancellationToken ct)
    {
        var (recentes, _) = await _despesas.SearchAsync(null, null, null, false, false, inicioMes.AddMonths(-4), inicioMes.AddMonths(1).AddTicks(-1), true, 1, Max, ct);
        static string Chave(Despesa d) => (string.IsNullOrWhiteSpace(d.Fornecedor) ? d.Descricao : d.Fornecedor!).Trim().ToLowerInvariant();
        return recentes.GroupBy(Chave)
            .Select(g =>
            {
                var ultima = g.OrderByDescending(d => d.Data).First();
                var registado = doMes.Where(d => Chave(d) == g.Key).Sum(d => d.ValorCents / 100m);
                return new FinancasFixaDto(string.IsNullOrWhiteSpace(ultima.Fornecedor) ? ultima.Descricao : ultima.Fornecedor!,
                    E(ultima.ValorCents / 100m), E(registado), registado > 0);
            })
            .OrderBy(f => f.Ok).ThenBy(f => f.Descricao).ToList();
    }

    private async Task<Tenant> TenantAsync(CancellationToken ct)
        => (_tenant.TenantId is { } id ? await _tenants.FindByIdAsync(id, ct) : null)
           ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

    private static void Validar(int ano, int mes)
    {
        if (ano is < 2000 or > 2100) throw new ValidationException("ano_invalido", "Ano inválido.");
        if (mes is < 1 or > 12) throw new ValidationException("mes_invalido", "Mês de 1 a 12.");
    }

    private static decimal E(decimal v) => IvaEngine.Euros(v);

    public static string NomeCategoria(DespesaCategoria c) => c switch
    {
        DespesaCategoria.Pecas => "Peças",
        DespesaCategoria.Material => "Material",
        DespesaCategoria.Ferramenta => "Ferramentas",
        DespesaCategoria.Software => "Software",
        DespesaCategoria.Transporte => "Transportes/Combustível",
        DespesaCategoria.Comunicacoes => "Comunicações",
        DespesaCategoria.Marketing => "Marketing",
        DespesaCategoria.Servicos => "Serviços",
        DespesaCategoria.Renda => "Renda",
        DespesaCategoria.Seguros => "Seguros",
        DespesaCategoria.Combustivel => "Combustível",
        DespesaCategoria.PecasUsadas => "Peças usadas",
        DespesaCategoria.FerramentasIa => "Ferramentas IA",
        DespesaCategoria.Subscricoes => "Subscrições",
        DespesaCategoria.VpsCopier => "VPS/Copier",
        DespesaCategoria.PropFirms => "Prop firms",
        DespesaCategoria.Alimentacao => "Alimentação",
        DespesaCategoria.CafeRestaurante => "Café/Restaurante",
        DespesaCategoria.Casa => "Casa",
        DespesaCategoria.Ginasio => "Ginásio",
        DespesaCategoria.Suplementacao => "Suplementação",
        DespesaCategoria.Lazer => "Lazer",
        DespesaCategoria.Saude => "Saúde",
        DespesaCategoria.Vestuario => "Vestuário",
        DespesaCategoria.SegurancaSocial => "Segurança Social",
        DespesaCategoria.PagamentoIva => "IVA pago ao Estado",
        DespesaCategoria.PagamentoIrs => "IRS pago",
        _ => "Outras",
    };
}
