using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Common;

/// <summary>
/// Sprint 129: humanização do período de garantia no PDF. Casos canónicos DL 84/2021
/// (1095/730/540) têm label dedicada; o resto degradeia para meses ou dias.
/// </summary>
public class GarantiaPdfFormatTests
{
    [Theory]
    [InlineData(1095, "3 anos")]
    [InlineData(730, "2 anos")]
    [InlineData(540, "18 meses")]
    public void FormatPeriodo_Canonico_LabelDedicada(int dias, string esperado)
        => GarantiaPdfRenderer.FormatPeriodo(dias).Should().Be(esperado);

    [Theory]
    [InlineData(365, "1 anos")]      // ainda passa pelo case anos (1*365)
    [InlineData(1460, "4 anos")]
    [InlineData(90, "3 meses")]
    [InlineData(60, "2 meses")]
    public void FormatPeriodo_Multiplos_DegradeiaPorRange(int dias, string esperado)
        => GarantiaPdfRenderer.FormatPeriodo(dias).Should().Be(esperado);

    [Theory]
    [InlineData(45, "45 dias")]
    [InlineData(7, "7 dias")]
    [InlineData(31, "31 dias")]
    public void FormatPeriodo_NaoMultiplo_DiasRaw(int dias, string esperado)
        => GarantiaPdfRenderer.FormatPeriodo(dias).Should().Be(esperado);
}
