using FluentAssertions;
using RepairDesk.Services.Compras;
using Xunit;

namespace RepairDesk.Tests.Compras;

/// <summary>20/11 (Bruno): os portes da compra dividem-se por igual pelas unidades (antes era pelo valor).</summary>
public class PortesPorUnidadeTests
{
    [Fact]
    public void UmaSoUnidade_LevaOsPortesTodos() => CompraService.PortesPorUnidade(5m, 1).Should().Be(5m);

    [Fact]
    public void DivideIgual_MobileSentrix() => CompraService.PortesPorUnidade(6.95m, 2).Should().Be(3.475m);

    [Fact]
    public void SemPortes_Zero() => CompraService.PortesPorUnidade(0m, 3).Should().Be(0m);

    [Fact]
    public void SemIva_TiraOIvaDedutivel()
    {
        CompraService.PortesSemIvaPorUnidade(5.95m, 1.11m, 2).Should().Be(2.42m);
        CompraService.PortesSemIvaPorUnidade(6.95m, 0m, 2).Should().Be(3.475m);
    }
}
