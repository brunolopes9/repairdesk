using FluentAssertions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Rotina;

namespace RepairDesk.Tests.Rotina;

/// <summary>Sprint 626: visão panorâmica justa — conta o feito a qualquer hora, «saltado» não quebra, hoje ainda não conta como falha.</summary>
public class RotinaHistoricoTests
{
    private static readonly DateOnly Hoje = new(2031, 3, 20);

    private static RotinaItem Item(string t) => new()
    {
        Titulo = t, Tipo = RotinaTipo.Meditacao, Hora = new TimeOnly(7, 0), Dias = 127, Ativo = true,
        CreatedAt = new DateTime(2031, 3, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static RotinaRegisto Reg(RotinaItem i, DateOnly d, RotinaRegistoEstado e) => new() { RotinaItemId = i.Id, Dia = d, Estado = e };

    [Fact]
    public void Sequencias_SaltadoNaoQuebra_HojeAindaNaoConta()
    {
        var med = Item("Meditação");
        var regs = new List<RotinaRegisto>
        {
            Reg(med, Hoje.AddDays(-4), RotinaRegistoEstado.Feito),
            Reg(med, Hoje.AddDays(-3), RotinaRegistoEstado.Feito),
            Reg(med, Hoje.AddDays(-2), RotinaRegistoEstado.Saltado), // ajudar os pais
            Reg(med, Hoje.AddDays(-1), RotinaRegistoEstado.Feito),
        };
        var h = RotinaHistorico.Calcular([med], regs, Hoje, 7);
        var habito = h.Habitos.Single();
        habito.SequenciaAtual.Should().Be(3);
        h.Dias.Single(d => d.Dia == Hoje.AddDays(-2)).Cumprido.Should().BeTrue(); // tudo saltado com motivo: não é falha
        h.Dias.Single(d => d.Dia == Hoje.AddDays(-1)).Percent.Should().Be(100);
        h.Dias.Single(d => d.Dia == Hoje).Percent.Should().Be(0);
        h.DiasCumpridosSeguidos.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Dia_CumpridoCom80PorCento_EFeitoForaDeHorasConta()
    {
        var itens = Enumerable.Range(1, 5).Select(n => Item($"H{n}")).ToList();
        var ontem = Hoje.AddDays(-1);
        var regs = itens.Take(4).Select(i => Reg(i, ontem, RotinaRegistoEstado.Feito)).ToList();
        var h = RotinaHistorico.Calcular(itens, regs, Hoje, 7);
        var d = h.Dias.Single(x => x.Dia == ontem);
        d.Percent.Should().Be(80);
        d.Cumprido.Should().BeTrue();
    }
}
