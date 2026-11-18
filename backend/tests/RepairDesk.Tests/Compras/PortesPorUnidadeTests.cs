using FluentAssertions;
using RepairDesk.Services.Compras;
using Xunit;

namespace RepairDesk.Tests.Compras;

/// <summary>18/11 (Bruno): os portes da compra repartem-se pelas unidades, pelo valor de cada uma.</summary>
public class PortesPorUnidadeTests
{
    [Fact]
    public void UmaSoUnidade_LevaOsPortesTodos() => CompraService.PortesPorUnidade(27m, 5m, 27m).Should().Be(5m);

    [Fact]
    public void RepartePeloValor()
    {
        // 1 × 30 € + 2 × 10 € = 50 €; portes 5 € → 3 € para a de 30 € e 1 € para cada uma de 10 €.
        CompraService.PortesPorUnidade(30m, 5m, 50m).Should().Be(3m);
        CompraService.PortesPorUnidade(10m, 5m, 50m).Should().Be(1m);
    }

    [Fact]
    public void SemPortes_Zero() => CompraService.PortesPorUnidade(10m, 0m, 10m).Should().Be(0m);
}
