using FluentAssertions;
using RepairDesk.Services.Billing;

namespace RepairDesk.Tests.Billing;

/// <summary>
/// Sprint 136: lógica de construção de linhas Moloni (peças ao custo + mão-de-obra).
/// </summary>
public class ReparacaoBillingItemsBuilderTests
{
    [Fact]
    public void Build_OnePartFullCost_PlusMaoDeObra_Returns2Lines()
    {
        // Cenário real do Bruno: ecrã Samsung A15 a 46€ + 24€ MdO = 70€
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart("Ecrã Samsung A15", 1, 4600) };
        var lines = ReparacaoBillingItemsBuilder.Build("Samsung A15", parts, totalCents: 7000, vatPercent: 23m);
        lines.Should().NotBeNull().And.HaveCount(2);
        lines![0].Name.Should().Be("Ecrã Samsung A15");
        lines[0].UnitPriceCents.Should().Be(4600);
        lines[0].Quantity.Should().Be(1);
        lines[1].Name.Should().StartWith("Mão-de-obra");
        lines[1].UnitPriceCents.Should().Be(2400);
    }

    [Fact]
    public void Build_MultiplePartsAndQty_SumsCorrectly()
    {
        // 2 ecrãs a 30€ + 1 bateria a 15€ + 25€ MdO = 100€
        var parts = new[]
        {
            new ReparacaoBillingItemsBuilder.UsedPart("Ecrã", 2, 3000),
            new ReparacaoBillingItemsBuilder.UsedPart("Bateria", 1, 1500),
        };
        var lines = ReparacaoBillingItemsBuilder.Build("iPhone 11", parts, totalCents: 10000, vatPercent: 23m);
        lines.Should().HaveCount(3);
        var subtotal = lines!.Sum(l => l.Quantity * l.UnitPriceCents);
        subtotal.Should().Be(10000);
    }

    [Fact]
    public void Build_PartCostEqualsTotal_NoMaoDeObraLine()
    {
        // Peça custou exactamente o total — não inventa linha de 0€.
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart("Acessório", 1, 5000) };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 5000, vatPercent: 23m);
        lines.Should().HaveCount(1);
        lines![0].Name.Should().Be("Acessório");
    }

    [Fact]
    public void Build_NoParts_ReturnsNull()
    {
        // Sem peças → fallback à linha sintética antiga (caller decide).
        var lines = ReparacaoBillingItemsBuilder.Build("X", Array.Empty<ReparacaoBillingItemsBuilder.UsedPart>(), totalCents: 7000, vatPercent: 23m);
        lines.Should().BeNull();
    }

    [Fact]
    public void Build_PartsExceedTotal_ReturnsNull()
    {
        // Edge: peças custaram mais que o orçamento → ambíguo, devolve null para fallback.
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart("Ecrã caro", 1, 8000) };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 7000, vatPercent: 23m);
        lines.Should().BeNull();
    }

    [Fact]
    public void Build_ZeroCostPart_Skipped()
    {
        // Peça com custo 0 (talvez offerta?) — não polui o orçamento, ignorada.
        var parts = new[]
        {
            new ReparacaoBillingItemsBuilder.UsedPart("Oferta", 1, 0),
            new ReparacaoBillingItemsBuilder.UsedPart("Ecrã", 1, 4000),
        };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 7000, vatPercent: 23m);
        lines.Should().HaveCount(2);
        lines!.Should().NotContain(l => l.Name == "Oferta");
        lines!.Sum(l => l.Quantity * l.UnitPriceCents).Should().Be(7000);
    }

    [Fact]
    public void Build_LongPartName_Truncated()
    {
        var nomeLongo = new string('A', 150);
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart(nomeLongo, 1, 5000) };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 7000, vatPercent: 23m);
        lines![0].Name.Length.Should().BeLessThanOrEqualTo(80, "Moloni limita o tamanho da designação");
    }

    [Fact]
    public void Build_VatPercentPropagatedToAllLines()
    {
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart("Ecrã", 1, 4600) };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 7000, vatPercent: 23m);
        lines!.All(l => l.VatPercent == 23m).Should().BeTrue();
    }

    [Fact]
    public void Build_ZeroTotal_ReturnsNull()
    {
        var parts = new[] { new ReparacaoBillingItemsBuilder.UsedPart("Ecrã", 1, 4600) };
        var lines = ReparacaoBillingItemsBuilder.Build("X", parts, totalCents: 0, vatPercent: 23m);
        lines.Should().BeNull();
    }
}
