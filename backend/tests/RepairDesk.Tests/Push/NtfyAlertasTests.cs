using FluentAssertions;
using RepairDesk.API.HostedServices;

namespace RepairDesk.Tests.Push;

/// <summary>20/11: alertas ntfy (vigia de stock PS5) → push. Só eventos «message»; título por defeito; links só https.</summary>
public class NtfyAlertasTests
{
    [Fact]
    public void Ler_MensagemComTitulo()
    {
        var a = NtfyAlertasHostedService.Ler("""{"id":"x","time":1,"event":"message","topic":"ps5","title":"PS5 EM STOCK NA WORTEN - 549.99 EUR","message":"Corre!","click":"https://www.worten.pt/p"}""");
        a.Should().NotBeNull();
        a!.Value.Titulo.Should().Be("PS5 EM STOCK NA WORTEN - 549.99 EUR");
        a.Value.Texto.Should().Be("Corre!");
        a.Value.Url.Should().Be("https://www.worten.pt/p");
    }

    [Theory]
    [InlineData("""{"id":"x","event":"open","topic":"ps5"}""")]
    [InlineData("""{"id":"x","event":"keepalive","topic":"ps5"}""")]
    [InlineData("isto não é json")]
    public void Ler_IgnoraOResto(string linha) => NtfyAlertasHostedService.Ler(linha).Should().BeNull();

    [Fact]
    public void Ler_SemTitulo_E_LinkNaoHttps()
    {
        var a = NtfyAlertasHostedService.Ler("""{"event":"message","message":"teste","click":"javascript:alert(1)"}""");
        a!.Value.Titulo.Should().Be("Alerta");
        a.Value.Url.Should().BeNull();
    }
}
