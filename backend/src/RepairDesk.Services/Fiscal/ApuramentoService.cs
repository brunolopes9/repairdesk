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
    decimal BaseForaUe);

public sealed record ApuramentoDespesasDto(
    int Despesas,
    decimal Base,
    decimal IvaDedutivel,
    decimal IvaNaoDedutivel,
    decimal BaseAutoliquidacao,
    decimal IvaAutoliquidado);

public sealed record ApuramentoResultadoDto(
    decimal Vendas,
    decimal CustoPecasVendidas,
    decimal Gastos,
    decimal Resultado,
    decimal ComprasDeStock);

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
    /// <summary>O que falta tratar para o apuramento estar certo (vendas sem fatura, compras sem fatura, despesas sem IVA…).</summary>
    IReadOnlyList<ApuramentoPendenteDto> Pendentes);

public interface IApuramentoService
{
    Task<ApuramentoDto> TrimestreAsync(int ano, int trimestre, CancellationToken ct = default);
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

    public ApuramentoService(IVendaRepository vendas, ICompraRepository compras, IDespesaRepository despesas)
    {
        _vendas = vendas;
        _compras = compras;
        _despesas = despesas;
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
        decimal baseNac = 0, ivaNac = 0, ivaPortes = 0, baseUe = 0, baseForaUe = 0;
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
            var semIva = d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago / (1 + l.TaxaIvaCompra));
            var portesIva = d.PortesIva ?? 0m;
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
        var compras = new ApuramentoComprasDto(docs.Count, Euros(baseNac), Euros(ivaNac), Euros(ivaPortes), Euros(baseUe), ivaAutoUe, Euros(baseForaUe));

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

        var vendasSemIva = porTaxa.Sum(t => t.Base);
        var gastos = despesasDto.Base + despesasDto.IvaNaoDedutivel;
        var resultado = new ApuramentoResultadoDto(vendasSemIva, custoPecas, Euros(gastos), Euros(vendasSemIva - custoPecas - gastos),
            Euros(baseNac + baseUe + baseForaUe));

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
            Euros(ivaDedutivel), ivaAutoliquidado, Euros(saldo), resultado, campos,
            pendentes.OrderBy(p => p.Tipo).ThenBy(p => p.Data).ToList());
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
