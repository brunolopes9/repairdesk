using System.Text.Json;
using FluentAssertions;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.Tests.Fiscal;

/// <summary>
/// Testes de aceitação do SPEC compras §6 com os valores reais do Excel do Bruno.
/// Se estes números mudarem, o motor de IVA está errado — não os testes.
/// </summary>
public class IvaEngineSpecTests
{
    private const decimal Taxa = 0.23m;
    private static decimal E(decimal v) => IvaEngine.Euros(v);

    [Fact]
    public void Spec61_PecaNacional_Touch5s()
    {
        var u = IvaEngine.Unidade(precoPago: 10.95m, taxaIvaCompra: Taxa, lucro: 5m, taxaIvaVenda: Taxa, autoliquidacaoUe: false);

        E(u.CustoSemIva).Should().Be(8.90m);
        E(u.IvaPagoNaCompra).Should().Be(2.05m);
        E(u.PrecoVendaSemIva).Should().Be(13.90m);
        E(u.IvaDaVenda).Should().Be(3.20m);
        E(u.PrecoFinalComIva).Should().Be(17.10m);
        E(u.IvaAPagarEstado).Should().Be(1.15m);
        u.AutoliquidacaoUe.Should().Be(0m);
        E(u.LucroQueSobra).Should().Be(5.00m);
    }

    [Fact]
    public void Spec62_PecaUe_DisplayS23()
    {
        var u = IvaEngine.Unidade(91.95m, 0m, 5m, Taxa, autoliquidacaoUe: true);

        E(u.CustoSemIva).Should().Be(91.95m);
        u.IvaPagoNaCompra.Should().Be(0m);
        E(u.PrecoVendaSemIva).Should().Be(96.95m);
        E(u.IvaDaVenda).Should().Be(22.30m);
        E(u.PrecoFinalComIva).Should().Be(119.25m);
        E(u.IvaAPagarEstado).Should().Be(22.30m);
        E(u.AutoliquidacaoUe).Should().Be(21.15m);
        E(u.LucroQueSobra).Should().Be(5.00m);
    }

    [Fact]
    public void Spec62_Negativo_PrecoDaContaMentalNacionalDaPrejuizoNaUe()
    {
        // "91,95 + 5 × 1,23 = 98,10" está ERRADO para peças UE: o motor nunca sugere este preço.
        var u = IvaEngine.Unidade(91.95m, 0m, 5m, Taxa, autoliquidacaoUe: true);
        E(u.PrecoFinalComIva).Should().NotBe(98.10m);

        var vendaErrada = IvaEngine.VendaAPreco(1, 98.10m, 91.95m, 0m, Taxa);
        E(vendaErrada.Lucro).Should().Be(-12.19m);
    }

    [Fact]
    public void Spec63_VendaAPrecoManual_PocoF3A100()
    {
        var v = IvaEngine.VendaAPreco(quantidade: 1, precoUnitarioCobradoComIva: 100m, precoPago: 16.42m, taxaIvaCompra: 0m, taxaIvaVenda: Taxa);

        E(v.ValorCobradoSemIva).Should().Be(81.30m);
        E(v.IvaDaVenda).Should().Be(18.70m);
        v.IvaPagoNaCompra.Should().Be(0m);
        E(v.IvaAPagarEstado).Should().Be(18.70m);
        E(v.CustoDasPecas).Should().Be(16.42m);
        E(v.Lucro).Should().Be(64.88m);
    }

    [Fact]
    public void Spec64_LinhaComQuantidade_VidrosA13()
    {
        var precoPago = 0.60m * 1.23m; // Tudo4mobile mostra S/IVA; guarda-se sem arredondar
        precoPago.Should().Be(0.738m);
        (3 * precoPago).Should().Be(2.214m);
        E(3 * IvaEngine.Unidade(precoPago, Taxa, 1m, Taxa, false).CustoSemIva).Should().Be(1.80m);
    }

    [Fact]
    public void Spec65_ReconciliacaoMicrowire()
    {
        var r = IvaEngine.Reconciliar(totalLinhasPago: 15.21m + 2.57m, portesPagos: 3.92m, totalDocumento: 21.70m);
        r.Diferenca.Should().Be(0.00m);
        r.TemDiferenca.Should().BeFalse();

        IvaEngine.Reconciliar(17.78m, 3.92m, 21.75m).TemDiferenca.Should().BeTrue();
        IvaEngine.Reconciliar(17.78m, 3.92m, null).Diferenca.Should().BeNull();
    }

    [Fact]
    public void Spec66_TotaisDoDataset()
    {
        var (linhas, documentos, ivaDespesas) = LoadDataset();

        var stock = ResumoIva.EmStock(linhas, Taxa);
        stock.Unidades.Should().Be(131);
        E(stock.ValorPecasPago).Should().Be(1608.68m);
        E(stock.ValorCobradoComIva).Should().Be(2509.97m);
        E(stock.IvaDaVenda).Should().Be(469.34m);
        E(stock.IvaJaPagoFornecedores).Should().Be(87.05m);
        E(stock.IvaAPagarEstado).Should().Be(382.29m);
        E(stock.IvaAPagarNacionais).Should().Be(52.44m);
        E(stock.IvaAPagarUe).Should().Be(329.85m);
        E(stock.Lucro).Should().Be(519.00m);

        var hoje = ResumoIva.Posicao(linhas, documentos, ivaLiquidadoVendas: 0m, ivaDespesas, Taxa);
        E(hoje.AutoliquidacaoUe).Should().Be(262.92m);
        E(hoje.IvaPortesNacionais).Should().Be(3.74m);
        E(hoje.IvaDespesasDedutiveis).Should().Be(41.76m);
        E(hoje.Saldo).Should().Be(-132.55m);

        var seVenderTudo = ResumoIva.Posicao(linhas, documentos, stock.IvaDaVenda, ivaDespesas, Taxa);
        E(seVenderTudo.Saldo).Should().Be(336.79m);
    }

    [Fact]
    public void Spec66_Consistencia_LucroEIvaBatemComAsFormulasFechadas()
    {
        var (linhas, _, _) = LoadDataset();
        var stock = ResumoIva.EmStock(linhas, Taxa);

        // Σ (qty × lucro) = lucro das peças
        E(stock.Lucro).Should().Be(E(linhas.Sum(l => l.Quantidade * l.Lucro)));

        // IVA a pagar = 23% × lucro total + 23% × custo das peças UE
        var custoUe = linhas.Where(l => l.TaxaIvaCompra == 0).Sum(l => l.Quantidade * l.PrecoPago);
        E(custoUe).Should().Be(1143.15m);
        E(stock.IvaAPagarEstado).Should().Be(E(Taxa * stock.Lucro + Taxa * custoUe));
    }

    [Fact]
    public void JaVendido_UmaPecaNacional_SaiDoStockEEntraNoVendido()
    {
        var linha = new ResumoIva.Linha(1, 1, 0, 10.95m, Taxa, 5m, false);

        ResumoIva.EmStock([linha], Taxa).Unidades.Should().Be(0);
        var vendido = ResumoIva.JaVendido([linha], Taxa);
        E(vendido.ValorCobradoComIva).Should().Be(17.10m);
        E(vendido.IvaAPagarEstado).Should().Be(1.15m);
        E(vendido.Lucro).Should().Be(5.00m);
    }

    private static (List<ResumoIva.Linha> Linhas, List<ResumoIva.Documento> Documentos, decimal IvaDespesas) LoadDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fiscal", "Fixtures", "spec-compras-dataset.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var linhas = root.GetProperty("linhas").EnumerateArray().Select(l => new ResumoIva.Linha(
            Quantidade: l.GetProperty("qtd").GetInt32(),
            QuantidadeVendida: 0,
            QuantidadeAbatida: 0,
            PrecoPago: l.GetProperty("preco").GetDecimal(),
            TaxaIvaCompra: l.GetProperty("taxa").GetDecimal(),
            Lucro: l.GetProperty("lucro").GetDecimal(),
            AutoliquidacaoUe: l.GetProperty("ue").GetBoolean())).ToList();

        var documentos = root.GetProperty("documentos").EnumerateArray().Select(d => new ResumoIva.Documento(
            d.GetProperty("portes").GetDecimal(), d.GetProperty("ivaPortes").GetDecimal())).ToList();

        var ivaDespesas = root.GetProperty("despesas").EnumerateArray()
            .Where(d => d.GetProperty("dedutivel").GetBoolean())
            .Sum(d =>
            {
                var valor = d.GetProperty("valor").GetDecimal();
                return valor - valor / (1 + d.GetProperty("taxa").GetDecimal());
            });

        return (linhas, documentos, ivaDespesas);
    }
}
