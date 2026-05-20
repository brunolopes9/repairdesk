using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Common;

/// <summary>
/// Sprint 134: parser Tudo4Mobile. Casos reais do dogfooding do Bruno
/// (encomenda 161144 Samsung A15 Touch+Display+Frame, total 47,20 €).
/// </summary>
public class SupplierPdfParserTests
{
    /// <summary>
    /// Texto fidelidade real do PDF Gmail do Bruno — descrição quebrada em 2 linhas,
    /// preço unitário sem IVA/portes, total com tudo separado em baixo.
    /// </summary>
    private const string Tudo4MobileSampleText = @"Bruno Lopes <bruno.miguel.martins.lopes@gmail.com>
Tudo4Mobile - Order 161144
Tudo4Mobile <geral@tudo4mobile.pt> 19 de maio de 2026 às 17:46
Thank you for your interest in Tudo4Mobile products.
Order Details
Order ID: 161144
Date Added: 19/05/2026
Payment Method: Multibanco
Total to pay: 47.20€
Product Model Quantity Price Total
Touch+Display+Frame Samsung Galaxy A15 4G/A155/A15 5G/A156 Service Pack
Black 148118 1 33.90€ 33.90€
Sub-Total (Without Tax): 33.90€
Express Shipping: 5.50€
VAT (23%): 7.80€
Total: 47.20€";

    [Fact]
    public void ParseTudo4Mobile_DetectsSupplierAndOrderId()
    {
        var r = SupplierPdfParser.Parse(Tudo4MobileSampleText);
        r.SupplierName.Should().Be("Tudo4Mobile");
        r.OrderId.Should().Be("161144");
        r.TotalCents.Should().Be(4720); // 47.20 €
        r.Confidence.Should().Be(ParseConfidence.High);
        r.DateAdded.Should().Be(new DateTime(2026, 5, 19));
    }

    [Fact]
    public void ParseTudo4Mobile_JoinsBrokenDescriptionLines()
    {
        var r = SupplierPdfParser.Parse(Tudo4MobileSampleText);
        r.Items.Should().HaveCount(1);
        var item = r.Items[0];
        // Antes do Sprint 134, ficava "Black 148118 1" (só a 2ª linha). Agora junta a anterior.
        item.Description.Should().Contain("Touch+Display+Frame");
        item.Description.Should().Contain("Samsung Galaxy A15");
        item.Description.Should().Contain("Service Pack");
        item.Description.Should().Contain("Black");
        // Não deve incluir o supplier SKU (148118) nem a qty (1) — esses são extraídos à parte.
        item.Description.Should().NotEndWith("148118");
        item.Description.Should().NotEndWith(" 1");
    }

    [Fact]
    public void ParseTudo4Mobile_ExtractsBrandModel()
    {
        var r = SupplierPdfParser.Parse(Tudo4MobileSampleText);
        var item = r.Items[0];
        item.Brand.Should().Be("Samsung");
        item.Model.Should().NotBeNullOrEmpty();
        item.Model!.Should().Contain("Galaxy A15");
    }

    [Fact]
    public void ParseTudo4Mobile_CostIncludesShippingAndVat()
    {
        var r = SupplierPdfParser.Parse(Tudo4MobileSampleText);
        var item = r.Items[0];
        // Subtotal 33.90 ÷ 33.90 × 47.20 = 47.20. Custo unitário com IVA + portes.
        item.LineTotalCents.Should().Be(4720, "1 unidade → custo unitário = total da encomenda");
    }

    [Fact]
    public void ParseTudo4Mobile_ExtractsQuantity()
    {
        var r = SupplierPdfParser.Parse(Tudo4MobileSampleText);
        r.Items[0].Quantity.Should().Be(1);
    }

    [Fact]
    public void ParseTudo4Mobile_MultipleItems_DistributesCostProportionally()
    {
        // Cenário sintético: 2 items, subtotal 50 + 30 = 80, portes 5, IVA 19.55 → total 104.55
        // Item A: 50/80 × 104.55 = 65.34
        // Item B: 30/80 × 104.55 = 39.21
        const string text = @"Order ID: 999999
Date Added: 20/05/2026
Touch+Display+Frame Samsung Galaxy S23
Black 100001 1 50.00€ 50.00€
Battery Apple iPhone 14
OEM 100002 1 30.00€ 30.00€
Sub-Total (Without Tax): 80.00€
Express Shipping: 5.00€
VAT (23%): 19.55€
Total: 104.55€";
        var r = SupplierPdfParser.Parse(text);
        r.SupplierName.Should().BeNull("não menciona 'tudo4mobile'"); // generic parser
        r.Items.Should().HaveCount(2);
        var sumAjustado = r.Items.Sum(i => i.LineTotalCents);
        sumAjustado.Should().BeInRange(10453, 10456, "soma dos custos unitários ≈ total da encomenda");
    }

    [Fact]
    public void Parse_EmptyText_ReturnsNone()
    {
        var r = SupplierPdfParser.Parse("");
        r.Confidence.Should().Be(ParseConfidence.None);
        r.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("47,20", 4720)] // PT format
    [InlineData("47.20", 4720)] // EN format
    [InlineData("1.234,56", 123456)] // PT thousands
    [InlineData("1,234.56", 123456)] // EN thousands
    public void ParseTudo4Mobile_HandlesPtAndEnNumberFormats(string totalRaw, int expectedCents)
    {
        var text = $@"Order ID: 1
Date Added: 19/05/2026
Item Brand Model 123 1 33.90€ 33.90€
Sub-Total (Without Tax): 33.90€
Total: {totalRaw}€";
        var r = SupplierPdfParser.Parse(text);
        r.TotalCents.Should().Be(expectedCents);
    }
}
