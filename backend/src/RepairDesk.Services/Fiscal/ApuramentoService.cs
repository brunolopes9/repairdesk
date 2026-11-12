using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Fiscal;

public sealed record ApuramentoTaxaDto(decimal Taxa, decimal Base, decimal Iva, int Linhas);

public sealed record ApuramentoComprasDto(
    int Documentos,
    decimal BaseNacional,
    decimal IvaNacional,
    decimal IvaPortes,
    decimal BaseUe,
    decimal IvaAutoliquidadoUe,
    decimal BaseForaUe,
    /// <summary>Sprint 576: compras UE com o IVA do país do fornecedor (não dedutível, custo = total).</summary>
    decimal BaseComIvaEstrangeiro = 0m);

public sealed record ApuramentoDespesasDto(
    int Despesas,
    decimal Base,
    decimal IvaDedutivel,
    decimal IvaNaoDedutivel,
    decimal BaseAutoliquidacao,
    decimal IvaAutoliquidado);

public sealed record ApuramentoResultadoDto(
    /// <summary>Vendas faturadas da LOJA (sem IVA). O trading vem à parte.</summary>
    decimal Vendas,
    decimal CustoPecasVendidas,
    decimal Gastos,
    /// <summary>Resultado da loja = vendas − custo das peças vendidas − despesas.</summary>
    decimal Resultado,
    decimal ComprasDeStock,
    /// <summary>Sprint 575: rendimentos de trading faturados no trimestre (fora do resultado da loja).</summary>
    decimal Trading = 0m);

/// <summary>
/// Sprint 575: estimativas (ano até ao fim do trimestre escolhido). Ver <see cref="EstimativasFiscais"/>.
/// </summary>
/// <summary>
/// Sprint 576: o que se assume nas estimativas (definido pelo dono, nunca adivinhado). Só conta a faturação
/// registada no Mender — desde julho de 2026 (3.º trimestre); o que está para trás não entra (decisão do Bruno).
/// </summary>
/// <remarks>Sprint 580: campos novos opcionais — null mantém o valor guardado (clientes antigos não os apagam).</remarks>
public sealed record PressupostosRequest(
    DateTime? InicioAtividade,
    decimal? SsContribuicaoAtual,
    int? IrsJovemAno = null,
    decimal? DeducoesColeta = null,
    bool? ServicosNaTabela151 = null,
    bool? TradingNaTabela151 = null,
    /// <summary>true = deixa de ter IRS Jovem (limpa o ano); false = segue IrsJovemAno.</summary>
    bool SemIrsJovem = false,
    /// <summary>Sprint 622: pro rata provisória (%) e data de efeitos; SemProRata limpa (deixa de ser misto).</summary>
    decimal? ProRataPercent = null,
    DateTime? ProRataInicio = null,
    bool SemProRata = false,
    /// <summary>Sprint 624: data em que o trading deixou o código da tabela do art. 151.º (0,75 → 0,35).</summary>
    DateTime? TradingTabela151Ate = null,
    bool SemTradingTabela151Ate = false);

/// <summary>Sprint 580: lembrete fiscal com prazo (ex.: rever o código de atividade do trading).</summary>
public sealed record LembreteFiscalDto(string Codigo, string Titulo, string Texto, bool Urgente);

public sealed record ApuramentoFechoDto(decimal IvaLiquidado, decimal IvaDedutivel, decimal Saldo, bool Pago, DateTime FechadoEm,
    /// <summary>Saldo recalculado agora − saldo declarado (≠ 0 = algo mudou depois de declarar).</summary>
    decimal Diferenca);

/// <summary>
/// SaldoDeclarado: o valor final que foi à declaração (negativo = crédito a reportar). Vazio = o calculado agora.
/// Serve também para registar trimestres declarados fora do Mender (ex.: 2.º trimestre de 2026: −243,50 €).
/// </summary>
public sealed record FecharTrimestreRequest(int Ano, int Trimestre, bool Pago = true, decimal? SaldoDeclarado = null);

public sealed record ApuramentoEstimativasDto(
    /// <summary>Null = ainda não indicada: sem redução de coeficientes e sem isenção de Segurança Social.</summary>
    DateTime? InicioAtividade,
    bool InicioDefinido,
    decimal ReceitasBens,
    /// <summary>Sprint 580: informática/software (sem trading), ao coeficiente CoefServicos.</summary>
    decimal ReceitasInformatica,
    /// <summary>Reparações, ao coeficiente CoefServicos.</summary>
    decimal ReceitasReparacoes,
    decimal ReceitasTrading,
    /// <summary>Fator nos coeficientes de serviços (0,5 no 1.º ano, 0,75 no 2.º, 1 depois).</summary>
    decimal FatorInicio,
    decimal DespesasJustificadas,
    decimal AcrescimoRegra15,
    decimal RendimentoTributavel,
    decimal IrsEstimado,
    decimal TaxaEfetiva,
    int AnoTabelaIrs,
    decimal SsRendimentoRelevante,
    decimal SsContribuicaoMensal,
    bool SsIsento,
    DateTime? SsIsentoAte,
    decimal? SsContribuicaoAtual = null,
    /// <summary>Sprint 580: coeficientes usados (0,75 se o código está na tabela do art. 151.º, senão 0,35).</summary>
    decimal CoefServicos = EstimativasFiscais.CoefOutrosServicos,
    decimal CoefTrading = EstimativasFiscais.CoefServicosTabela,
    bool ServicosNaTabela151 = false,
    bool TradingNaTabela151 = true,
    int? IrsJovemAno = null,
    decimal IrsJovemPercentagem = 0m,
    decimal IrsJovemIsencao = 0m,
    decimal ColetaAntesDeducoes = 0m,
    decimal? DeducoesColeta = null,
    /// <summary>Sprint 622: sujeito passivo misto — pro rata provisória (%) e data de efeitos.</summary>
    decimal? ProRataPercent = null,
    DateTime? ProRataInicio = null,
    /// <summary>Sprint 624: até quando o trading foi a 0,75 e quanto do trading desse período.</summary>
    DateTime? TradingTabela151Ate = null,
    decimal ReceitasTradingTabela151 = 0m);

/// <summary>Campo da declaração periódica de IVA (Continente) — a confirmar com o contabilista.</summary>
public sealed record ApuramentoCampoDto(string Campo, string Descricao, decimal Valor);

public sealed record ApuramentoPendenteDto(string Tipo, Guid Id, string Descricao, decimal Valor, DateTime Data);

public sealed record ApuramentoDto(
    int Ano,
    int Trimestre,
    DateTime De,
    DateTime Ate,
    IReadOnlyList<ApuramentoTaxaDto> VendasPorTaxa,
    int Faturas,
    decimal IvaLiquidado,
    ApuramentoComprasDto Compras,
    ApuramentoDespesasDto Despesas,
    decimal IvaDedutivel,
    decimal IvaAutoliquidado,
    /// <summary>Positivo = a entregar ao Estado; negativo = a recuperar (reporte).</summary>
    decimal Saldo,
    ApuramentoResultadoDto Resultado,
    IReadOnlyList<ApuramentoCampoDto> Campos,
    ApuramentoEstimativasDto Estimativas,
    /// <summary>O que falta tratar para o apuramento estar certo (vendas sem fatura, compras sem fatura, despesas sem IVA…).</summary>
    IReadOnlyList<ApuramentoPendenteDto> Pendentes,
    /// <summary>Sprint 576: vendas a 0% a clientes da UE (vão também à declaração recapitulativa) e de fora da UE.</summary>
    decimal VendasIsentasUe = 0m,
    decimal VendasIsentasForaUe = 0m,
    ApuramentoFechoDto? Fecho = null,
    /// <summary>Sprint 578: crédito de IVA do trimestre anterior (excesso a reportar) que abate a este.</summary>
    decimal Reporte = 0m,
    /// <summary>Saldo − reporte: o que se paga (positivo) ou fica a crédito para o próximo (negativo).</summary>
    decimal SaldoFinal = 0m,
    /// <summary>Sprint 616: vendas entregues marcadas «tratada fora do Mender» no trimestre (não entram no IVA do Mender).</summary>
    int ForaApuramentoVendas = 0,
    decimal ForaApuramentoTotal = 0m);

public interface IApuramentoService
{
    Task<ApuramentoDto> TrimestreAsync(int ano, int trimestre, CancellationToken ct = default);
    Task DefinirPressupostosAsync(PressupostosRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<LembreteFiscalDto>> LembretesAsync(CancellationToken ct = default);
    Task MarcarCodigoTradingRevistoAsync(bool revisto, CancellationToken ct = default);
    Task<ApuramentoFechoDto> FecharAsync(FecharTrimestreRequest req, CancellationToken ct = default);
    Task ReabrirAsync(int ano, int trimestre, CancellationToken ct = default);
}

/// <summary>
/// Sprint 573 (Doc 94 Fase 5): apuramento do IVA e resultado de um trimestre, para a declaração periódica.
/// <list type="bullet">
/// <item>IVA liquidado = vendas FATURADAS com data da fatura no trimestre, por taxa (preços gravados com IVA).</item>
/// <item>IVA dedutível = compras nacionais com fatura (peças + portes) + despesas (IVA indicado × % dedutível).</item>
/// <item>Autoliquidação (compras UE e serviços intra-UE): liquida e deduz o mesmo valor — efeito 0, mas declara-se.</item>
/// <item>Resultado = vendas sem IVA − custo das peças vendidas − despesas (sem o IVA dedutível). Comprar stock não é gasto.</item>
/// </list>
/// Nada é inventado: o que falta (fatura por emitir, compra sem fatura, despesa sem IVA) vai para "Pendentes".
/// </summary>
public class ApuramentoService : IApuramentoService
{
    private readonly IVendaRepository _vendas;
    private readonly ICompraRepository _compras;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly IApuramentoFechoRepository? _fechos;
    private readonly IFornecedorRepository? _fornecedores;

    public ApuramentoService(IVendaRepository vendas, ICompraRepository compras, IDespesaRepository despesas,
        ITenantRepository tenants, ITenantContext tenant, IApuramentoFechoRepository? fechos = null,
        IFornecedorRepository? fornecedores = null)
    {
        _fechos = fechos;
        _fornecedores = fornecedores;
        _vendas = vendas;
        _compras = compras;
        _despesas = despesas;
        _tenants = tenants;
        _tenant = tenant;
    }

    public async Task DefinirPressupostosAsync(PressupostosRequest req, CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        if (req.InicioAtividade is { } d && (d.Year < 1990 || d.Year > 2100))
            throw new ValidationException("data_invalida", "Data de início de atividade inválida.");
        if (req.SsContribuicaoAtual is < 0 or > 100_000)
            throw new ValidationException("valor_invalido", "Contribuição mensal inválida.");
        if (req.IrsJovemAno is < 1 or > 10)
            throw new ValidationException("irs_jovem_invalido", "Ano do IRS Jovem de 1 a 10.");
        if (req.DeducoesColeta is < 0 or > 100_000)
            throw new ValidationException("valor_invalido", "Deduções à coleta inválidas.");
        t.InicioAtividade = req.InicioAtividade?.Date;
        t.SsContribuicaoAtual = req.SsContribuicaoAtual;
        if (req.SemIrsJovem) t.IrsJovemAno = null;
        else if (req.IrsJovemAno is { } anoJovem) t.IrsJovemAno = anoJovem;
        if (req.DeducoesColeta is { } deducoes) t.DeducoesColeta = deducoes;
        if (req.ServicosNaTabela151 is { } servTabela) t.ServicosNaTabela151 = servTabela;
        if (req.TradingNaTabela151 is { } tradTabela) t.TradingNaTabela151 = tradTabela;
        if (req.SemTradingTabela151Ate) t.TradingTabela151Ate = null;
        else if (req.TradingTabela151Ate is { } tradAte) t.TradingTabela151Ate = tradAte.Date;
        if (req.SemProRata)
        {
            t.ProRataPercent = null;
            t.ProRataInicio = null;
        }
        else if (req.ProRataPercent is { } pr)
        {
            if (pr is < 0 or > 100) throw new ValidationException("pro_rata_invalida", "Pro rata de 0 a 100%.");
            if (req.ProRataInicio is not { } inicioPr) throw new ValidationException("pro_rata_inicio", "Indica desde quando vale a pro rata.");
            t.ProRataPercent = pr;
            t.ProRataInicio = inicioPr.Date;
        }
        await _tenants.SaveAsync(ct);
    }

    public async Task<ApuramentoFechoDto> FecharAsync(FecharTrimestreRequest req, CancellationToken ct = default)
    {
        if (_fechos is null) throw new InvalidOperationException("Fechos indisponíveis.");
        var r = await TrimestreAsync(req.Ano, req.Trimestre, ct);
        var fecho = await _fechos.FindAsync(req.Ano, req.Trimestre, ct);
        if (fecho is null)
        {
            fecho = new ApuramentoFecho { Ano = req.Ano, Trimestre = req.Trimestre };
            await _fechos.AddAsync(fecho, ct);
        }
        fecho.IvaLiquidado = r.IvaLiquidado;
        fecho.IvaDedutivel = r.IvaDedutivel;
        // Guarda o valor final declarado (já com o crédito do trimestre anterior) — é o que passa ao seguinte.
        fecho.Saldo = req.SaldoDeclarado ?? r.SaldoFinal;
        fecho.Pago = req.Pago;
        fecho.FechadoEm = DateTime.UtcNow;
        await _fechos.SaveAsync(ct);
        return new ApuramentoFechoDto(fecho.IvaLiquidado, fecho.IvaDedutivel, fecho.Saldo, fecho.Pago, fecho.FechadoEm, 0m);
    }

    public async Task ReabrirAsync(int ano, int trimestre, CancellationToken ct = default)
    {
        if (_fechos is null) return;
        if (await _fechos.FindAsync(ano, trimestre, ct) is { } fechado)
        {
            _fechos.Remove(fechado);
            await _fechos.SaveAsync(ct);
        }
    }

    /// <summary>
    /// Sprint 580: lembretes fiscais com prazo. Hoje: quem fatura trading num código da tabela do art. 151.º (ex.: CIRS
    /// 1519, coeficiente 0,75) é lembrado de rever o código em janeiro (CAE fora da tabela → 0,35), até confirmar.
    /// </summary>
    public async Task<IReadOnlyList<LembreteFiscalDto>> LembretesAsync(CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        var lembretes = new List<LembreteFiscalDto>();
        if (!t.TradingNaTabela151 || t.CodigoTradingRevistoEm is not null || t.TradingTabela151Ate is not null) return lembretes;

        var hoje = DateTime.UtcNow;
        var desde = new DateTime(hoje.Year - 1, 1, 1);
        var temTrading = (await _vendas.ListParaApuramentoAsync(ParaUtc(desde), ParaUtc(hoje).AddDays(1), ct))
            .Any(v => v.Atividade == VendaAtividade.Trading && v.InvoiceNumber is not null);
        if (!temTrading) return lembretes;

        var janeiro = hoje.Month == 1 ? hoje.Year : hoje.Year + 1;
        lembretes.Add(new LembreteFiscalDto(
            "codigo_trading",
            $"Janeiro de {janeiro}: rever o código de atividade do trading",
            "O trading está num código da tabela do art. 151.º (ex.: CIRS 1519), tributado a 0,75 no regime simplificado. "
            + "Num CAE fora da tabela (ex.: 66190, atividades auxiliares de serviços financeiros) passa a 0,35 — menos de metade do IRS. "
            + "Faz-se com uma declaração de alterações no Portal das Finanças, com efeitos a 1 de janeiro. Confirma o código com o contabilista antes.",
            Urgente: hoje.Month == 1));
        return lembretes;
    }

    public async Task MarcarCodigoTradingRevistoAsync(bool revisto, CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        t.CodigoTradingRevistoEm = revisto ? DateTime.UtcNow : null;
        await _tenants.SaveAsync(ct);
    }

    private async Task<Tenant> TenantAsync(CancellationToken ct)
        => (_tenant.TenantId is { } id ? await _tenants.FindByIdAsync(id, ct) : null)
            ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

    private static decimal Taxa => FiscalDefaults.TaxaIvaNormal;

    /// <summary>
    /// Sprint 622 (Doc 98 §4.1): fração do IVA de uma despesa que se deduz.
    /// Trading → 0; LopesTech → % da despesa; Mista → % da despesa × pro rata, só a partir de ProRataInicio
    /// (antes disso a mista deduz como LopesTech — tratamento anterior, sem pro rata).
    /// </summary>
    public static decimal FracaoDedutivel(AfetacaoIva afetacao, int deducaoPercent, DateTime data, decimal? proRataPercent, DateTime? proRataInicio)
    {
        var base100 = Math.Clamp(deducaoPercent, 0, 100) / 100m;
        return afetacao switch
        {
            AfetacaoIva.Trading => 0m,
            AfetacaoIva.Mista when proRataPercent is { } pr && proRataInicio is { } ini && data.Date >= ini.Date => base100 * pr / 100m,
            _ => base100,
        };
    }

    public async Task<ApuramentoDto> TrimestreAsync(int ano, int trimestre, CancellationToken ct = default)
    {
        if (trimestre is < 1 or > 4) throw new ValidationException("trimestre_invalido", "Trimestre de 1 a 4.");
        if (ano is < 2000 or > 2100) throw new ValidationException("ano_invalido", "Ano inválido.");
        var de = new DateTime(ano, (trimestre - 1) * 3 + 1, 1);
        var ate = de.AddMonths(3);
        var pendentes = new List<ApuramentoPendenteDto>();

        // ---- vendas ----
        // A fatura é um instante em UTC: o trimestre conta-se à hora de Lisboa.
        var vendas = await _vendas.ListParaApuramentoAsync(ParaUtc(de).AddDays(-1), ParaUtc(ate).AddDays(1), ct);
        var noTrimestre = vendas.Where(v => DentroDe(DeLisboa(v.InvoiceEmittedAt ?? v.Data), de, ate)).ToList();
        var faturadas = noTrimestre.Where(v => v.InvoiceNumber is not null).ToList();
        var fora = noTrimestre.Where(v => v.InvoiceNumber is null && v.ForaApuramento && v.Estado == VendaEstado.Entregue).ToList();
        foreach (var v in noTrimestre.Where(v => v.InvoiceNumber is null && !v.ForaApuramento && v.Estado == VendaEstado.Entregue && v.Items.Any(i => !i.PorFora)))
            pendentes.Add(new("venda_sem_fatura", v.Id, $"Venda #{v.Numero}{(v.Cliente is { } c ? $" · {c.Nome}" : string.Empty)}",
                v.TotalCents / 100m, DeLisboa(v.Data)));

        // Sprint 642: as linhas «por fora» não estão na fatura — ficam fora do IVA.
        var linhas = faturadas.SelectMany(v => v.Items.Where(i => !i.PorFora)).ToList();
        var porTaxa = linhas
            .GroupBy(i => i.IvaRate)
            .Select(g =>
            {
                var total = g.Sum(i => i.TotalCents) / 100m;
                var baseTrib = g.Sum(i => i.TotalCents / 100m / (1 + i.IvaRate / 100m));
                return new ApuramentoTaxaDto(g.Key, Euros(baseTrib), Euros(total - baseTrib), g.Count());
            })
            .OrderByDescending(t => t.Taxa)
            .ToList();
        var ivaLiquidadoVendas = porTaxa.Sum(t => t.Iva);
        var custoPecas = Euros(linhas.Where(i => i.CustoUnitarioPago is not null)
            .Sum(i => i.Quantidade * i.CustoUnitarioPago!.Value / (1 + FracaoTaxa(i.TaxaIvaCompra))));

        // ---- compras (stock) ----
        var docs = await _compras.ListDocumentosPeriodoAsync(de, ate, ct);
        decimal baseNac = 0, ivaNac = 0, ivaPortes = 0, baseUe = 0, baseForaUe = 0, baseComIvaLocal = 0;
        foreach (var d in docs)
        {
            var regime = d.Fornecedor?.RegimeIva ?? RegimeIvaFornecedor.Nacional;
            var pago = d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago);
            if (regime == RegimeIvaFornecedor.UeAutoliquidacao)
            {
                baseUe += pago + d.PortesPagos;
                continue;
            }
            if (regime == RegimeIvaFornecedor.ForaUe)
            {
                baseForaUe += pago + d.PortesPagos;
                continue;
            }
            if (regime == RegimeIvaFornecedor.UeComIvaLocal)
            {
                baseComIvaLocal += pago + d.PortesPagos;
                continue;
            }
            var semIva = d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago / (1 + l.TaxaIvaCompra));
            var portesIva = IvaEngine.PortesIvaNacional(d.PortesPagos, d.PortesIva);
            if (d.FaturaEmFalta)
            {
                // Sem fatura o IVA não é dedutível: conta como custo e fica a aviso.
                pendentes.Add(new("compra_sem_fatura", d.Id, $"Compra #{d.Numero} · {d.Fornecedor?.Name ?? "—"} (IVA {Euros(pago - semIva + portesIva):0.00} € por deduzir)",
                    Euros(pago + d.PortesPagos), d.Data));
                continue;
            }
            baseNac += semIva + d.PortesPagos - portesIva;
            ivaNac += pago - semIva;
            ivaPortes += portesIva;
        }
        var ivaAutoUe = Euros(baseUe * Taxa);
        var compras = new ApuramentoComprasDto(docs.Count, Euros(baseNac), Euros(ivaNac), Euros(ivaPortes), Euros(baseUe), ivaAutoUe, Euros(baseForaUe), Euros(baseComIvaLocal));

        // ---- despesas ----
        // Sprint 633: despesas pessoais e obrigações (SS, IVA/IRS pagos) não entram no apuramento.
        // Sprint 644: nem as que não têm fatura com o NIF da empresa (só gestão).
        var despesas = (await _despesas.ListPeriodoAsync(de, ate, ct)).Where(d => d.Ambito == DespesaAmbito.Negocio && !d.SemFaturaValida).ToList();
        var tenantFiscal = await TenantAsync(ct);
        // Sprint 648 (F6b): regime do fornecedor da despesa, pelo nome — distingue autoliquidação UE (campos 16/17)
        // de fora da UE (campos 3/4 + Quadro 06-A campo 98).
        var regimes = _fornecedores is null ? new Dictionary<string, RegimeIvaFornecedor>()
            : (await _fornecedores.ListByTenantAsync(true, ct)).GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().RegimeIva, StringComparer.OrdinalIgnoreCase);
        decimal baseDesp = 0, ivaDed = 0, ivaNaoDed = 0, baseAuto = 0, ivaAutoDedDesp = 0;
        decimal baseAutoServUe = 0, baseAutoForaUe = 0, ded20 = 0, ded24 = 0;
        foreach (var d in despesas)
        {
            var investimento = d.Natureza == DespesaNatureza.Investimento;
            var valor = d.ValorCents / 100m;
            if (d.Categoria is DespesaCategoria.Pecas or DespesaCategoria.PecasUsadas)
                pendentes.Add(new("despesa_pecas", d.Id, $"{d.Descricao} — peças como despesa: confirma que não está também em Compras", valor, d.Data));
            var fracao = FracaoDedutivel(d.Afetacao, d.DeducaoPercent, d.Data, tenantFiscal.ProRataPercent, tenantFiscal.ProRataInicio);
            if (d.ReverseCharge)
            {
                // Autoliquidação: liquida-se tudo; deduz-se conforme a afetação (Trading 0, Mista × pro rata).
                var ivaAuto = valor * Taxa;
                baseAuto += valor;
                baseDesp += valor;
                ivaAutoDedDesp += ivaAuto * fracao;
                ivaNaoDed += ivaAuto - ivaAuto * fracao;
                var foraUe = d.Fornecedor is { } fn && regimes.TryGetValue(fn.Trim(), out var rg) && rg == RegimeIvaFornecedor.ForaUe;
                if (foraUe) baseAutoForaUe += valor; else baseAutoServUe += valor;
                if (investimento) ded20 += ivaAuto * fracao; else ded24 += ivaAuto * fracao;
                continue;
            }
            if (d.IvaCents is not { } ivaCents)
            {
                // Sem IVA indicado: conta o total como gasto e não deduz nada até ser preenchido.
                pendentes.Add(new("despesa_sem_iva", d.Id, $"{d.Descricao}{(d.Fornecedor is { } f ? $" · {f}" : string.Empty)}", valor, d.Data));
                baseDesp += valor;
                continue;
            }
            var iva = ivaCents / 100m;
            var dedutivel = Euros(iva * fracao);
            ivaDed += dedutivel;
            if (investimento) ded20 += dedutivel; else ded24 += dedutivel;
            ivaNaoDed += iva - dedutivel;
            baseDesp += valor - iva;
        }
        var ivaAutoDesp = Euros(baseAuto * Taxa);
        var despesasDto = new ApuramentoDespesasDto(despesas.Count, Euros(baseDesp), Euros(ivaDed), Euros(ivaNaoDed), Euros(baseAuto), ivaAutoDesp);

        // ---- totais ----
        var ivaAutoliquidado = ivaAutoUe + ivaAutoDesp;
        var ivaLiquidado = ivaLiquidadoVendas + ivaAutoliquidado;
        // Compras de stock UE são sempre LopesTech (deduz tudo); as despesas em autoliquidação deduzem pela afetação.
        var ivaDedutivel = compras.IvaNacional + compras.IvaPortes + despesasDto.IvaDedutivel + ivaAutoUe + Euros(ivaAutoDedDesp);
        var saldo = ivaLiquidado - ivaDedutivel;

        // Sprint 575: o trading entra no IVA (a declaração é uma por NIF) mas não no resultado da loja.
        var trading = Euros(faturadas.Where(v => v.Atividade == VendaAtividade.Trading).SelectMany(v => v.Items.Where(i => !i.PorFora)).Sum(BaseSemIva));
        var vendasSemIva = porTaxa.Sum(t => t.Base) - trading;
        var gastos = despesasDto.Base + despesasDto.IvaNaoDedutivel;
        var resultado = new ApuramentoResultadoDto(vendasSemIva, custoPecas, Euros(gastos), Euros(vendasSemIva - custoPecas - gastos),
            Euros(baseNac + baseUe + baseForaUe + baseComIvaLocal), trading);
        var estimativas = await EstimativasAsync(ano, de, ate, ct);

        // Sprint 576: vendas a 0% pelo país do cliente (prefixo do NIF): UE vai também à declaração recapitulativa.
        decimal isentasUe = 0, isentasFora = 0;
        foreach (var v in faturadas)
            foreach (var i in v.Items.Where(i => i.IvaRate == 0 && !i.PorFora))
            {
                if (ClienteUe(v.Cliente?.Nif)) isentasUe += BaseSemIva(i); else isentasFora += BaseSemIva(i);
            }
        // Sprint 578: crédito do trimestre anterior (declarado com saldo negativo) abate a este.
        var (anoAnt, triAnt) = trimestre == 1 ? (ano - 1, 4) : (ano, trimestre - 1);
        var anterior = _fechos is null ? null : await _fechos.FindAsync(anoAnt, triAnt, ct);
        var reporte = anterior is { Saldo: < 0 } ? -anterior.Saldo : 0m;
        var saldoFinal = Euros(saldo - reporte);

        ApuramentoFechoDto? fechoDto = null;
        if (_fechos is not null && await _fechos.FindAsync(ano, trimestre, ct) is { } fechado)
            fechoDto = new ApuramentoFechoDto(fechado.IvaLiquidado, fechado.IvaDedutivel, fechado.Saldo, fechado.Pago, fechado.FechadoEm, Euros(saldoFinal - fechado.Saldo));

        decimal BaseTaxa(decimal t) => porTaxa.Where(x => x.Taxa == t).Sum(x => x.Base);
        decimal IvaTaxa(decimal t) => porTaxa.Where(x => x.Taxa == t).Sum(x => x.Iva);
        // Sprint 648 (F6b, regras do Claude fiscal — Contexto/specs/F6b): campos numerados da declaração periódica.
        // Vendas a 0%: serviço B2B a SP da UE → 7; fora da UE/exportação → 8; payouts de prop firm da UE → 9.
        decimal c7 = 0, c8 = 0, c9 = 0;
        foreach (var v in faturadas)
            foreach (var i in v.Items.Where(i => i.IvaRate == 0 && !i.PorFora))
            {
                var b = BaseSemIva(i);
                if (!ClienteUe(v.Cliente?.Nif)) c8 += b;
                else if (v.Atividade == VendaAtividade.Trading) c9 += b;
                else c7 += b;
            }
        var c3 = BaseTaxa(23) + Euros(baseAutoForaUe);
        var c4 = IvaTaxa(23) + Euros(baseAutoForaUe * Taxa);
        var campos = new List<ApuramentoCampoDto>
        {
            new("1", "Base tributável à taxa reduzida (6%)", BaseTaxa(6)),
            new("2", "IVA liquidado à taxa reduzida (6%)", IvaTaxa(6)),
            new("5", "Base tributável à taxa intermédia (13%)", BaseTaxa(13)),
            new("6", "IVA liquidado à taxa intermédia (13%)", IvaTaxa(13)),
            new("3", "Base tributável à taxa normal (23%) — vendas + serviços de fora da UE em autoliquidação", Euros(c3)),
            new("4", "IVA liquidado à taxa normal (23%)", Euros(c4)),
            new("7", "Transmissões/serviços isentos com direito à dedução — clientes UE (vai também à declaração recapitulativa)", Euros(c7)),
            new("8", "Operações isentas — clientes fora da UE / exportações", Euros(c8)),
            new("9", "Operações isentas sem direito à dedução — payouts UE (confirmar com a contabilista)", Euros(c9)),
            new("12", "Aquisições intracomunitárias de bens (base)", Euros(baseUe)),
            new("13", "IVA das aquisições intracomunitárias de bens (23%)", Euros(baseUe * Taxa)),
            new("16", "Serviços adquiridos a fornecedores da UE em autoliquidação (base)", Euros(baseAutoServUe)),
            new("17", "IVA dos serviços UE autoliquidados (23%)", Euros(baseAutoServUe * Taxa)),
            new("20", "IVA dedutível — imobilizado (despesas marcadas «Investimento»)", Euros(ded20)),
            new("22", "IVA dedutível — existências (peças: nacionais + UE autoliquidadas + portes)", Euros(ivaNac + ivaPortes + ivaAutoUe)),
            new("24", "IVA dedutível — outros bens e serviços (despesas)", Euros(ded24)),
            new("06", baseAutoForaUe > 0 ? "Quadro 06 — liquidou imposto como adquirente: SIM" : "Quadro 06 — liquidou imposto como adquirente: NÃO", baseAutoForaUe > 0 ? 1 : 0),
            new("98", "Quadro 06-A — base das aquisições de serviços a fornecedores de fora da UE", Euros(baseAutoForaUe)),
        };

        return new ApuramentoDto(ano, trimestre, de, ate.AddDays(-1), porTaxa, faturadas.Count, Euros(ivaLiquidado), compras, despesasDto,
            Euros(ivaDedutivel), ivaAutoliquidado, Euros(saldo), resultado, campos, estimativas,
            pendentes.OrderBy(p => p.Tipo).ThenBy(p => p.Data).ToList(), Euros(isentasUe), Euros(isentasFora), fechoDto,
            Euros(reporte), saldoFinal, fora.Count, Euros(fora.Sum(v => v.TotalCents) / 100m));
    }

    private static decimal BaseSemIva(VendaItem i) => i.TotalCents / 100m / (1 + i.IvaRate / 100m);

    private static readonly HashSet<string> PaisesUe = ["AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "GR", "ES", "FI", "FR", "HR",
        "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "RO", "SE", "SI", "SK", "XI"];

    /// <summary>Cliente de outro país da UE pelo prefixo do NIF/VAT (ex.: "CZ…" FTMO). Sem prefixo UE = fora da UE.</summary>
    private static bool ClienteUe(string? nif)
    {
        var n = (nif ?? string.Empty).Trim().ToUpperInvariant();
        return n.Length > 2 && PaisesUe.Contains(n[..2]);
    }

    /// <summary>
    /// Sprint 575: IRS e Segurança Social estimados. IRS: faturação do ano até ao fim do trimestre × coeficientes
    /// (peças vendidas = bens 0,15; reparação = 0,35; software/informática e trading = 0,75), com a redução dos
    /// primeiros anos e a regra dos 15%. Segurança Social: sobre o trimestre escolhido (paga-se nos 3 meses seguintes).
    /// </summary>
    private async Task<ApuramentoEstimativasDto> EstimativasAsync(int ano, DateTime de, DateTime ate, CancellationToken ct)
    {
        var t = _tenant.TenantId is { } id ? await _tenants.FindByIdAsync(id, ct) : null;
        // Sprint 576: o início de atividade é indicado pelo dono — nunca adivinhado pela 1.ª fatura (errava a redução
        // do IRS e a isenção da Segurança Social). Sem data: sem redução e sem isenção (estimativa prudente).
        var inicio = t?.InicioAtividade;
        var inicioAno = new DateTime(ano, 1, 1);

        var vendasAno = (await _vendas.ListParaApuramentoAsync(ParaUtc(inicioAno).AddDays(-1), ParaUtc(ate).AddDays(1), ct))
            .Where(v => v.InvoiceNumber is not null && DentroDe(DeLisboa(v.InvoiceEmittedAt ?? v.Data), inicioAno, ate)).ToList();
        decimal bens = 0, s075 = 0, s035 = 0, trading = 0, tradingAntes = 0, bensTrim = 0, servTrim = 0;
        var tradingAte = t?.TradingTabela151Ate;
        foreach (var v in vendasAno)
        {
            var noTrimestre = DentroDe(DeLisboa(v.InvoiceEmittedAt ?? v.Data), de, ate);
            foreach (var i in v.Items.Where(i => !i.PorFora))
            {
                var b = BaseSemIva(i);
                var ehBem = v.Atividade == VendaAtividade.Loja && (i.CompraLinhaId is not null || v.Tipo == VendaTipo.Produto);
                if (v.Atividade == VendaAtividade.Trading)
                {
                    trading += b;
                    if (tradingAte is { } limite && DeLisboa(v.InvoiceEmittedAt ?? v.Data).Date <= limite.Date) tradingAntes += b;
                }
                else if (ehBem) bens += b;
                else if (v.Tipo == VendaTipo.Reparacao) s035 += b;
                else s075 += b;
                if (noTrimestre) { if (ehBem) bensTrim += b; else servTrim += b; }
            }
        }

        var fator = inicio is { } ini ? EstimativasFiscais.FatorInicioAtividade(ano, ini) : 1m;
        // Sprint 580: o coeficiente depende do código onde a atividade está registada (tabela do art. 151.º → 0,75; CAE
        // fora da tabela → 0,35) — indicado pelo dono, nunca adivinhado. Informática e reparações vão juntas (CAE).
        var servicosNaTabela = t?.ServicosNaTabela151 ?? false;
        var tradingNaTabela = t?.TradingNaTabela151 ?? true;
        var coefServicos = servicosNaTabela ? EstimativasFiscais.CoefServicosTabela : EstimativasFiscais.CoefOutrosServicos;
        var coefTrading = tradingNaTabela ? EstimativasFiscais.CoefServicosTabela : EstimativasFiscais.CoefOutrosServicos;
        // Sprint 624: com data de mudança, o trading até essa data vai a 0,75 e o resto a 0,35.
        var (trading075, trading035) = tradingAte is not null
            ? (tradingAntes, trading - tradingAntes)
            : tradingNaTabela ? (trading, 0m) : (0m, trading);
        // Regra dos 15% (art. 31.º n.º 13): sobre os rendimentos de serviços a 0,75 e a 0,35 (Doc 98 §4.1).
        var receitas075 = s075 + s035 + trading;
        var justificadas = EstimativasFiscais.DeducaoEspecifica
            + (await _despesas.ListPeriodoAsync(inicioAno, ate, ct)).Where(d => d.Ambito == DespesaAmbito.Negocio && !d.SemFaturaValida).Sum(d => d.ValorCents / 100m)
            + (await _compras.ListDocumentosPeriodoAsync(inicioAno, ate, ct)).Sum(d => d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago) + d.PortesPagos);
        var acrescimo = Math.Max(0m, EstimativasFiscais.Regra15 * receitas075 - justificadas);
        var rendimento = bens * EstimativasFiscais.CoefBens
            + (s075 + s035) * coefServicos * fator
            + (trading075 * EstimativasFiscais.CoefServicosTabela + trading035 * EstimativasFiscais.CoefOutrosServicos) * fator
            + acrescimo;
        // Sprint 580: o IRS Jovem isenta parte do rendimento; as deduções à coleta (e-fatura) abatem ao imposto até 0.
        var isencaoJovem = EstimativasFiscais.IrsJovemIsencao(rendimento, t?.IrsJovemAno);
        var coleta = EstimativasFiscais.Irs(rendimento - isencaoJovem, ano);
        var irs = Math.Max(0m, coleta - (t?.DeducoesColeta ?? 0m));
        var faturacao = bens + s075 + s035 + trading;

        var relevante = EstimativasFiscais.SsServicos * servTrim + EstimativasFiscais.SsBens * bensTrim;
        var isentoAte = inicio?.Date.AddMonths(12);
        // Quem já paga contribuições (ex.: 115,87 €/mês) não está isento, mesmo dentro dos 12 meses.
        var isento = isentoAte is { } fim && ate <= fim && t?.SsContribuicaoAtual is null or 0m;

        return new ApuramentoEstimativasDto(inicio?.Date, inicio is not null, Euros(bens), Euros(s075), Euros(s035), Euros(trading),
            fator, Euros(justificadas), Euros(acrescimo), Euros(rendimento), Euros(irs),
            faturacao > 0 ? Math.Round(irs / faturacao, 4) : 0m, EstimativasFiscais.AnoTabela(ano),
            Euros(relevante), isento ? 0m : Euros(relevante / 3m * EstimativasFiscais.SsTaxa), isento, isentoAte,
            t?.SsContribuicaoAtual,
            coefServicos, coefTrading, servicosNaTabela, tradingNaTabela,
            t?.IrsJovemAno, EstimativasFiscais.IrsJovemPercentagem(t?.IrsJovemAno), Euros(isencaoJovem), Euros(coleta), t?.DeducoesColeta, t?.ProRataPercent, t?.ProRataInicio, tradingAte, Euros(trading075));
    }

    /// <summary>Taxa da compra guardada como fração (0,23); tolera valores antigos em % (23).</summary>
    private static decimal FracaoTaxa(decimal? t) => t is not { } v ? 0m : v > 1 ? v / 100m : v;

    private static decimal Euros(decimal v) => IvaEngine.Euros(v);

    private static bool DentroDe(DateTime d, DateTime de, DateTime ate) => d >= de && d < ate;

    private static TimeZoneInfo? Lisboa()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"); }
        catch (TimeZoneNotFoundException) { return null; }
    }

    private static DateTime DeLisboa(DateTime utc)
        => Lisboa() is { } tz ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz) : utc;

    private static DateTime ParaUtc(DateTime local)
        => Lisboa() is { } tz ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz) : local;
}
