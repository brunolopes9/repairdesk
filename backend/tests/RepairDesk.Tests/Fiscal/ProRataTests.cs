using FluentAssertions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.Tests.Fiscal;

/// <summary>Sprint 622 (Doc 98 §4.1): quanto IVA de uma despesa se deduz num sujeito passivo misto.</summary>
public class ProRataTests
{
    private static readonly DateTime Inicio = new(2026, 11, 9);

    [Fact]
    public void Trading_NuncaDeduz() =>
        ApuramentoService.FracaoDedutivel(AfetacaoIva.Trading, 100, new DateTime(2026, 12, 1), 20m, Inicio).Should().Be(0m);

    [Fact]
    public void LopesTech_DeduzAPercentagemDaDespesa() =>
        ApuramentoService.FracaoDedutivel(AfetacaoIva.LopesTech, 50, new DateTime(2026, 12, 1), 20m, Inicio).Should().Be(0.5m);

    [Fact]
    public void Mista_ProRataSoDesdeOInicio()
    {
        ApuramentoService.FracaoDedutivel(AfetacaoIva.Mista, 100, new DateTime(2026, 11, 9), 20m, Inicio).Should().Be(0.2m);
        ApuramentoService.FracaoDedutivel(AfetacaoIva.Mista, 100, new DateTime(2026, 10, 15), 20m, Inicio).Should().Be(1m);
        ApuramentoService.FracaoDedutivel(AfetacaoIva.Mista, 100, new DateTime(2026, 12, 1), null, null).Should().Be(1m);
    }
}
