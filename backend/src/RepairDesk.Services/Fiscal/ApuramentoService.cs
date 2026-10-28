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
/// <summary>Sprint 576: faturação sem IVA antes do Mender (ex.: janeiro a junho), por tipo.</summary>
public sealed record FaturacaoAnteriorDto(decimal Bens = 0, decimal Informatica = 0, decimal Reparacoes = 0, decimal Trading = 0);

/// <summary>Sprint 576: o que se assume nas estimativas (definido pelo dono, nunca adivinhado).</summary>
public sealed record PressupostosRequest(DateTime? InicioAtividade, decimal? SsContribuicaoAtual, int? Ano = null, FaturacaoAnteriorDto? FaturacaoAnterior = null);

public sealed record ApuramentoFechoDto(decimal IvaLiquidado, decimal IvaDedutivel, decimal Saldo, bool Pago, DateTime FechadoEm,
    /// <summary>Saldo recalculado agora − saldo declarado (≠ 0 = algo mudou depois de declarar).</summary>
    decimal Diferenca);

public sealed record FecharTrimestreRequest(int Ano, int Trimestre, bool Pago = true);

public sealed record ApuramentoEstimativasDto(
    /// <summary>Null = ainda não indicada: sem redução de coeficientes e sem isenção de Segurança Social.</summary>
    DateTime? InicioAtividade,
    bool InicioDefinido,
    decimal ReceitasBens,
    decimal ReceitasServicos075,
    decimal ReceitasServicos035,
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
    FaturacaoAnteriorDto? FaturacaoAnterior = null);

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
    ApuramentoFechoDto? Fecho = null);

public interface IApuramentoService
{
    Task<ApuramentoDto> TrimestreAsync(int ano, int trimestre, CancellationToken ct = default);
    Task DefinirPressupostosAsync(PressupostosRequest req, CancellationToken ct = default);
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

    public ApuramentoService(IVendaRepository vendas, ICompraRepository compras, IDespesaRepository despesas,
        ITenantRepository tenants, ITenantContext tenant, IApuramentoFechoRepository? fechos = null)
    {
        _fechos = fechos;
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
        t.InicioAtividade = req.InicioAtividade?.Date;
        t.SsContribuicaoAtual = req.SsContribuicaoAtual;
        if (req.Ano is { } ano && req.FaturacaoAnterior is { } fa)
        {
            if (new[] { fa.Bens, fa.Informatica, fa.Reparacoes, fa.Trading }.Any(v => v is < 0 or > 10_000_000))
                throw new ValidationException("valor_invalido", "Faturação anterior inválida.");
            var todas = FaturacaoAnterior(t);
            todas[ano] = fa;
            t.FaturacaoAnteriorJson = System.Text.Json.JsonSerializer.Serialize(todas);
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
        fecho.Saldo = r.Saldo;
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

    private async Task<Tenant> TenantAsync(CancellationToken ct)
        => (_tenant.TenantId is { } id ? await _tenants.FindByIdAsync(id, ct) : null)
            ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

    private static Dictionary<int, FaturacaoAnteriorDto> FaturacaoAnterior(Tenant? t)
    {
        if (string.IsNullOrWhiteSpace(t?.FaturacaoAnteriorJson)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, FaturacaoAnteriorDto>>(t.FaturacaoAnteriorJson) ?? new(); }
        catch (System.Text.Json.JsonException) { return new(); }
    }

    private static decimal Taxa => FiscalDefaults.TaxaIvaNormal;

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
        foreach (var v in noTrimestre.Where(v => v.InvoiceNumber is null && v.Estado == VendaEstado.Entregue))
            pendentes.Add(new("venda_sem_fatura", v.Id, $"Venda #{v.Numero}{(v.Cliente is { } c ? $" · {c.Nome}" : string.Empty)}",
                v.TotalCents / 100m, DeLisboa(v.Data)));

        var linhas = faturadas.SelectMany(v => v.Items).ToList();
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
        var despesas = await _despesas.ListPeriodoAsync(de, ate, ct);
        decimal baseDesp = 0, ivaDed = 0, ivaNaoDed = 0, baseAuto = 0;
        foreach (var d in despesas)
        {
            var valor = d.ValorCents / 100m;
            if (d.Categoria is DespesaCategoria.Pecas or DespesaCategoria.PecasUsadas)
                pendentes.Add(new("despesa_pecas", d.Id, $"{d.Descricao} — peças como despesa: confirma que não está também em Compras", valor, d.Data));
            if (d.ReverseCharge)
            {
                baseAuto += valor;
                baseDesp += valor;
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
            var dedutivel = Euros(iva * Math.Clamp(d.DeducaoPercent, 0, 100) / 100m);
            ivaDed += dedutivel;
            ivaNaoDed += iva - dedutivel;
            baseDesp += valor - iva;
        }
        var ivaAutoDesp = Euros(baseAuto * Taxa);
        var despesasDto = new ApuramentoDespesasDto(despesas.Count, Euros(baseDesp), Euros(ivaDed), Euros(ivaNaoDed), Euros(baseAuto), ivaAutoDesp);

        // ---- totais ----
        var ivaAutoliquidado = ivaAutoUe + ivaAutoDesp;
        var ivaLiquidado = ivaLiquidadoVendas + ivaAutoliquidado;
        var ivaDedutivel = compras.IvaNacional + compras.IvaPortes + despesasDto.IvaDedutivel + ivaAutoliquidado;
        var saldo = ivaLiquidado - ivaDedutivel;

        // Sprint 575: o trading entra no IVA (a declaração é uma por NIF) mas não no resultado da loja.
        var trading = Euros(faturadas.Where(v => v.Atividade == VendaAtividade.Trading).SelectMany(v => v.Items).Sum(BaseSemIva));
        var vendasSemIva = porTaxa.Sum(t => t.Base) - trading;
        var gastos = despesasDto.Base + despesasDto.IvaNaoDedutivel;
        var resultado = new ApuramentoResultadoDto(vendasSemIva, custoPecas, Euros(gastos), Euros(vendasSemIva - custoPecas - gastos),
            Euros(baseNac + baseUe + baseForaUe + baseComIvaLocal), trading);
        var estimativas = await EstimativasAsync(ano, de, ate, ct);

        // Sprint 576: vendas a 0% pelo país do cliente (prefixo do NIF): UE vai também à declaração recapitulativa.
        decimal isentasUe = 0, isentasFora = 0;
        foreach (var v in faturadas)
            foreach (var i in v.Items.Where(i => i.IvaRate == 0))
            {
                if (ClienteUe(v.Cliente?.Nif)) isentasUe += BaseSemIva(i); else isentasFora += BaseSemIva(i);
            }
        ApuramentoFechoDto? fechoDto = null;
        if (_fechos is not null && await _fechos.FindAsync(ano, trimestre, ct) is { } fechado)
            fechoDto = new ApuramentoFechoDto(fechado.IvaLiquidado, fechado.IvaDedutivel, fechado.Saldo, fechado.Pago, fechado.FechadoEm, Euros(saldo - fechado.Saldo));

        decimal BaseTaxa(decimal t) => porTaxa.Where(x => x.Taxa == t).Sum(x => x.Base);
        decimal IvaTaxa(decimal t) => porTaxa.Where(x => x.Taxa == t).Sum(x => x.Iva);
        var campos = new List<ApuramentoCampoDto>
        {
            new("1", "Base tributável à taxa reduzida (6%)", BaseTaxa(6)),
            new("2", "IVA liquidado à taxa reduzida (6%)", IvaTaxa(6)),
            new("5", "Base tributável à taxa intermédia (13%)", BaseTaxa(13)),
            new("6", "IVA liquidado à taxa intermédia (13%)", IvaTaxa(13)),
            new("3", "Base tributável à taxa normal (23%)", BaseTaxa(23)),
            new("4", "IVA liquidado à taxa normal (23%)", IvaTaxa(23)),
            new("—", "Vendas sem IVA (0%, com motivo de isenção)", BaseTaxa(0)),
            new("—", "Aquisições intracomunitárias e serviços em autoliquidação (base)", Euros(baseUe + baseAuto)),
            new("—", "IVA autoliquidado (liquida e deduz — efeito 0 €)", ivaAutoliquidado),
            new("22", "IVA dedutível — existências à taxa normal (peças e portes nacionais)", Euros(ivaNac + ivaPortes)),
            new("24", "IVA dedutível — outros bens e serviços (despesas)", despesasDto.IvaDedutivel),
        };

        return new ApuramentoDto(ano, trimestre, de, ate.AddDays(-1), porTaxa, faturadas.Count, Euros(ivaLiquidado), compras, despesasDto,
            Euros(ivaDedutivel), ivaAutoliquidado, Euros(saldo), resultado, campos, estimativas,
            pendentes.OrderBy(p => p.Tipo).ThenBy(p => p.Data).ToList(), Euros(isentasUe), Euros(isentasFora), fechoDto);
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
        var anterior = FaturacaoAnterior(t).GetValueOrDefault(ano);

        var vendasAno = (await _vendas.ListParaApuramentoAsync(ParaUtc(inicioAno).AddDays(-1), ParaUtc(ate).AddDays(1), ct))
            .Where(v => v.InvoiceNumber is not null && DentroDe(DeLisboa(v.InvoiceEmittedAt ?? v.Data), inicioAno, ate)).ToList();
        decimal bens = 0, s075 = 0, s035 = 0, trading = 0, bensTrim = 0, servTrim = 0;
        foreach (var v in vendasAno)
        {
            var noTrimestre = DentroDe(DeLisboa(v.InvoiceEmittedAt ?? v.Data), de, ate);
            foreach (var i in v.Items)
            {
                var b = BaseSemIva(i);
                var ehBem = v.Atividade == VendaAtividade.Loja && (i.CompraLinhaId is not null || v.Tipo == VendaTipo.Produto);
                if (v.Atividade == VendaAtividade.Trading) trading += b;
                else if (ehBem) bens += b;
                else if (v.Tipo == VendaTipo.Reparacao) s035 += b;
                else s075 += b;
                if (noTrimestre) { if (ehBem) bensTrim += b; else servTrim += b; }
            }
        }

        if (anterior is not null)
        {
            bens += anterior.Bens;
            s075 += anterior.Informatica;
            s035 += anterior.Reparacoes;
            trading += anterior.Trading;
        }
        var fator = inicio is { } ini ? EstimativasFiscais.FatorInicioAtividade(ano, ini) : 1m;
        var receitas075 = s075 + trading;
        var justificadas = EstimativasFiscais.DeducaoEspecifica
            + (await _despesas.ListPeriodoAsync(inicioAno, ate, ct)).Sum(d => d.ValorCents / 100m)
            + (await _compras.ListDocumentosPeriodoAsync(inicioAno, ate, ct)).Sum(d => d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago) + d.PortesPagos);
        var acrescimo = Math.Max(0m, EstimativasFiscais.Regra15 * receitas075 - justificadas);
        var rendimento = bens * EstimativasFiscais.CoefBens
            + receitas075 * EstimativasFiscais.CoefServicosTabela * fator
            + s035 * EstimativasFiscais.CoefOutrosServicos * fator
            + acrescimo;
        var irs = EstimativasFiscais.Irs(rendimento, ano);
        var faturacao = bens + receitas075 + s035;

        var relevante = EstimativasFiscais.SsServicos * servTrim + EstimativasFiscais.SsBens * bensTrim;
        var isentoAte = inicio?.Date.AddMonths(12);
        var isento = isentoAte is { } fim && ate <= fim;

        return new ApuramentoEstimativasDto(inicio?.Date, inicio is not null, Euros(bens), Euros(s075), Euros(s035), Euros(trading),
            fator, Euros(justificadas), Euros(acrescimo), Euros(rendimento), Euros(irs),
            faturacao > 0 ? Math.Round(irs / faturacao, 4) : 0m, EstimativasFiscais.AnoTabela(ano),
            Euros(relevante), isento ? 0m : Euros(relevante / 3m * EstimativasFiscais.SsTaxa), isento, isentoAte,
            t?.SsContribuicaoAtual, anterior);
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
