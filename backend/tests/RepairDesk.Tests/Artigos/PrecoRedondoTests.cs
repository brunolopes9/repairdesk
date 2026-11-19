using FluentAssertions;
using RepairDesk.Services.Artigos;
using Xunit;

namespace RepairDesk.Tests.Artigos;

/// <summary>19/11: o preço calculado (custo + lucro do lote) sobe sempre para o ,90 seguinte — nada de 4,24 € na etiqueta.</summary>
public class PrecoRedondoTests
{
    [Theory]
    [InlineData(424, 490)]
    [InlineData(1833, 1890)]
    [InlineData(7614, 7690)]
    [InlineData(1890, 1890)]
    [InlineData(1895, 1990)]
    [InlineData(1900, 1990)]
    [InlineData(0, 0)]
    public void SobeParaONoventaSeguinte(int cents, int esperado)
        => ArtigoService.PrecoRedondo(cents).Should().Be(esperado);
}
