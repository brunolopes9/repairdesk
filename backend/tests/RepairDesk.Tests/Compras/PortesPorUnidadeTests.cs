using FluentAssertions;
using RepairDesk.Core.Entities;
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

    [Fact]
    public void MesmoEnvio_DivideOsPortesPorTodasAsFaturas()
    {
        // Tudo4Mobile 18/11: FRE A09 (7 un., sem portes) + FRE A01 (2 un., portes 5,95) → 5,95 / 9.
        var a09 = new CompraDocumento { PortesPagos = 0m, EnvioPortes = 5.95m, EnvioPortesIva = 1.11m, EnvioUnidades = 9 };
        var a01 = new CompraDocumento { PortesPagos = 5.95m, PortesIva = 1.11m, EnvioPortes = 5.95m, EnvioPortesIva = 1.11m, EnvioUnidades = 9 };
        CompraService.PortesDoDocumento(a09, 7).Should().Be(0.6611m);
        CompraService.PortesDoDocumento(a01, 2).Should().Be(0.6611m);
        CompraService.PortesSemIvaDoDocumento(a01, 2).Should().Be(0.5378m);
        // Sem envio preenchido: só o próprio documento.
        CompraService.PortesDoDocumento(new CompraDocumento { PortesPagos = 5.95m }, 2).Should().Be(2.975m);
    }
}
