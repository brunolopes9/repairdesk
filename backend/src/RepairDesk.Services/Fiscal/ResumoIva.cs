namespace RepairDesk.Services.Fiscal;

/// <summary>
/// Agregações do SPEC compras §4: margem "já vendido" vs "em stock (se vender tudo)" e a posição
/// real de IVA (declaração periódica). Puro — recebe linhas/documentos já carregados.
/// </summary>
public static class ResumoIva
{
    public sealed record Linha(int Quantidade, int QuantidadeVendida, int QuantidadeAbatida, decimal PrecoPago, decimal TaxaIvaCompra, decimal Lucro, bool AutoliquidacaoUe);

    public sealed record Documento(decimal PortesPagos, decimal? PortesIva);

    /// <summary>Coluna da tabela §4.1 (todos os valores já multiplicados pela quantidade).</summary>
    public sealed record Coluna(
        int Unidades,
        decimal ValorCobradoComIva,
        decimal ValorPecasPago,
        decimal IvaDaVenda,
        decimal IvaJaPagoFornecedores,
        decimal IvaAPagarEstado,
        decimal IvaAPagarNacionais,
        decimal IvaAPagarUe,
        decimal Lucro);

    /// <summary>
    /// Posição de IVA (§4.2). O IVA das compras e despesas deduz-se quando se compra, não quando
    /// se vende. A autoliquidação UE entra a liquidar e a deduzir — anula-se, mas é mostrada.
    /// </summary>
    public sealed record PosicaoIva(
        decimal IvaLiquidadoVendas,
        decimal AutoliquidacaoUe,
        decimal IvaComprasNacionais,
        decimal IvaPortesNacionais,
        decimal IvaDespesasDedutiveis,
        decimal Saldo);

    public static Coluna EmStock(IEnumerable<Linha> linhas, decimal taxaIvaVenda)
        => Somar(linhas.Select(l => (l, Math.Max(0, l.Quantidade - l.QuantidadeVendida - l.QuantidadeAbatida))), taxaIvaVenda);

    /// <summary>"Já vendido" ao preço sugerido. Quando as vendas guardarem snapshot, passa a usar o preço real.</summary>
    public static Coluna JaVendido(IEnumerable<Linha> linhas, decimal taxaIvaVenda)
        => Somar(linhas.Select(l => (l, l.QuantidadeVendida)), taxaIvaVenda);

    public static PosicaoIva Posicao(
        IEnumerable<Linha> linhasCompradas,
        IEnumerable<Documento> documentos,
        decimal ivaLiquidadoVendas,
        decimal ivaDespesasDedutiveis,
        decimal taxaIvaVenda)
    {
        decimal autoliq = 0, ivaNacional = 0;
        foreach (var l in linhasCompradas)
        {
            var u = IvaEngine.Unidade(l.PrecoPago, l.TaxaIvaCompra, l.Lucro, taxaIvaVenda, l.AutoliquidacaoUe);
            autoliq += l.Quantidade * u.AutoliquidacaoUe;
            ivaNacional += l.Quantidade * u.IvaPagoNaCompra;
        }
        var ivaPortes = documentos.Sum(d => d.PortesIva ?? 0m);

        // + liquidado nas vendas + autoliquidado − autoliquidado − compras nacionais − portes − despesas
        var saldo = ivaLiquidadoVendas + autoliq - autoliq - ivaNacional - ivaPortes - ivaDespesasDedutiveis;
        return new PosicaoIva(ivaLiquidadoVendas, autoliq, ivaNacional, ivaPortes, ivaDespesasDedutiveis, saldo);
    }

    private static Coluna Somar(IEnumerable<(Linha Linha, int Qtd)> itens, decimal taxa)
    {
        int unidades = 0;
        decimal cobrado = 0, pago = 0, ivaVenda = 0, ivaPago = 0, ivaAPagar = 0, ivaNac = 0, ivaUe = 0, lucro = 0;
        foreach (var (l, qtd) in itens)
        {
            if (qtd <= 0) continue;
            var u = IvaEngine.Unidade(l.PrecoPago, l.TaxaIvaCompra, l.Lucro, taxa, l.AutoliquidacaoUe);
            unidades += qtd;
            cobrado += qtd * u.PrecoFinalComIva;
            pago += qtd * l.PrecoPago;
            ivaVenda += qtd * u.IvaDaVenda;
            ivaPago += qtd * u.IvaPagoNaCompra;
            ivaAPagar += qtd * u.IvaAPagarEstado;
            if (l.TaxaIvaCompra > 0) ivaNac += qtd * u.IvaAPagarEstado; else ivaUe += qtd * u.IvaAPagarEstado;
            lucro += qtd * u.LucroQueSobra;
        }
        return new Coluna(unidades, cobrado, pago, ivaVenda, ivaPago, ivaAPagar, ivaNac, ivaUe, lucro);
    }
}
