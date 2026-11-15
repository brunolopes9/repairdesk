using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

public class LojaMudancasTests
{
    [Fact]
    public async Task Esperar_AcordaNoInstanteEmQueAlgoMuda()
    {
        var m = new LojaMudancas();
        var marca = m.Atual;
        var espera = m.EsperarAsync(marca, TimeSpan.FromSeconds(30), CancellationToken.None);
        espera.IsCompleted.Should().BeFalse();
        m.Sinalizar();
        (await Task.WhenAny(espera, Task.Delay(2000))).Should().BeSameAs(espera);
        m.Atual.Should().Be(marca + 1);
    }

    [Fact]
    public async Task Esperar_SeJaMudouNaoEspera_ESemMudancasAcabaNoLimite()
    {
        var m = new LojaMudancas();
        var marca = m.Atual;
        m.Sinalizar();
        await m.EsperarAsync(marca, TimeSpan.FromSeconds(30), CancellationToken.None); // volta logo

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        await m.EsperarAsync(m.Atual, TimeSpan.FromMilliseconds(150), CancellationToken.None);
        relogio.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(100);
    }
}
