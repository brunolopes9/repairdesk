namespace RepairDesk.Services.Fiscal;

/// <summary>
/// Motor de IVA das peças/artigos de stock — implementação literal do SPEC compras §3.
/// Funções puras em <see cref="decimal"/>, sem arredondamentos intermédios: arredonda-se só na
/// apresentação (SPEC §3.4). Todas as taxas vêm por parâmetro — nada de 23% hardcoded.
///
/// A mesma fórmula distingue nacional de UE sem "if":
///  - Nacional (já pagou IVA na compra): IVA a pagar = lucro × taxa.
///  - UE (comprou a 0%): IVA a pagar = (custo + lucro) × taxa — o IVA do custo não foi pago a ninguém.
/// A autoliquidação UE é liquidada E deduzida na compra (efeito 0 €). NÃO se volta a descontar
/// na venda — isso seria deduzir IVA que nunca foi pago (SPEC §3.2, teste negativo §6.2).
/// </summary>
public static class IvaEngine
{
    /// <summary>Cálculo por UNIDADE de uma linha de compra vendida ao preço sugerido (SPEC §3.1 + §3.2).</summary>
    public static CalculoUnidade Unidade(decimal precoPago, decimal taxaIvaCompra, decimal lucro, decimal taxaIvaVenda, bool autoliquidacaoUe)
    {
        var custoSemIva = precoPago / (1 + taxaIvaCompra);
        var ivaPagoNaCompra = precoPago - custoSemIva;
        var autoliquidacao = autoliquidacaoUe && taxaIvaCompra == 0 ? custoSemIva * taxaIvaVenda : 0m;
        var precoVendaSemIva = custoSemIva + lucro;
        var ivaDaVenda = precoVendaSemIva * taxaIvaVenda;
        var precoFinalComIva = precoVendaSemIva + ivaDaVenda;
        var ivaAPagar = ivaDaVenda - ivaPagoNaCompra;
        var lucroQueSobra = precoFinalComIva - precoPago - ivaAPagar;

        return new CalculoUnidade(
            CustoSemIva: custoSemIva,
            IvaPagoNaCompra: ivaPagoNaCompra,
            AutoliquidacaoUe: autoliquidacao,
            LucroSemIva: lucro,
            LucroComIva: lucro * (1 + taxaIvaVenda),
            PrecoVendaSemIva: precoVendaSemIva,
            IvaDaVenda: ivaDaVenda,
            PrecoFinalComIva: precoFinalComIva,
            IvaAPagarEstado: ivaAPagar,
            LucroQueSobra: lucroQueSobra);
    }

    /// <summary>Venda a um preço escolhido (diferente do sugerido) — SPEC §3.3.</summary>
    public static CalculoVenda VendaAPreco(int quantidade, decimal precoUnitarioCobradoComIva, decimal precoPago, decimal taxaIvaCompra, decimal taxaIvaVenda)
        => VendaPorTotal(quantidade, quantidade * precoUnitarioCobradoComIva, precoPago, taxaIvaCompra, taxaIvaVenda);

    /// <summary>Igual a <see cref="VendaAPreco"/> mas a partir do total cobrado da linha (já com desconto).</summary>
    public static CalculoVenda VendaPorTotal(int quantidade, decimal valorCobradoComIva, decimal precoPago, decimal taxaIvaCompra, decimal taxaIvaVenda)
    {
        var valorCobradoSemIva = valorCobradoComIva / (1 + taxaIvaVenda);
        var ivaDaVenda = valorCobradoComIva - valorCobradoSemIva;
        var ivaPagoNaCompraUnit = precoPago - precoPago / (1 + taxaIvaCompra);
        var ivaAPagar = ivaDaVenda - quantidade * ivaPagoNaCompraUnit;
        var custoDasPecas = quantidade * precoPago;
        var lucro = valorCobradoComIva - custoDasPecas - ivaAPagar;

        return new CalculoVenda(valorCobradoComIva, valorCobradoSemIva, ivaDaVenda, quantidade * ivaPagoNaCompraUnit,
            ivaAPagar, custoDasPecas, lucro);
    }

    /// <summary>Reconciliação de um documento: linhas + portes vs total impresso (SPEC §2.2, tolerância 0,01 €).</summary>
    public static Reconciliacao Reconciliar(decimal totalLinhasPago, decimal portesPagos, decimal? totalDocumento)
    {
        var calculado = totalLinhasPago + portesPagos;
        if (totalDocumento is null) return new Reconciliacao(calculado, null, false);
        var diferenca = Math.Round(totalDocumento.Value - calculado, 2, MidpointRounding.AwayFromZero);
        return new Reconciliacao(calculado, diferenca, Math.Abs(diferenca) > ToleranciaReconciliacao);
    }

    public const decimal ToleranciaReconciliacao = 0.01m;

    /// <summary>Arredondamento de apresentação (2 casas, half away from zero — como o Excel).</summary>
    public static decimal Euros(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}

public sealed record CalculoUnidade(
    decimal CustoSemIva,
    decimal IvaPagoNaCompra,
    decimal AutoliquidacaoUe,
    decimal LucroSemIva,
    decimal LucroComIva,
    decimal PrecoVendaSemIva,
    decimal IvaDaVenda,
    decimal PrecoFinalComIva,
    decimal IvaAPagarEstado,
    decimal LucroQueSobra);

public sealed record CalculoVenda(
    decimal ValorCobradoComIva,
    decimal ValorCobradoSemIva,
    decimal IvaDaVenda,
    decimal IvaPagoNaCompra,
    decimal IvaAPagarEstado,
    decimal CustoDasPecas,
    decimal Lucro);

public sealed record Reconciliacao(decimal TotalCalculado, decimal? Diferenca, bool TemDiferenca);
