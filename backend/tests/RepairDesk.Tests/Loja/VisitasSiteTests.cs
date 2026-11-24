using FluentAssertions;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Loja;
using Xunit;

namespace RepairDesk.Tests.Loja;

/// <summary>23/11: visitas do site — origem, dispositivo, bots e as contas do relatório.</summary>
public class VisitasSiteTests
{
    [Theory]
    [InlineData("https://www.google.com/", null, "google")]
    [InlineData("https://l.facebook.com/l.php?u=x", null, "facebook")]
    [InlineData(null, "fbclid=abc", "facebook")]
    [InlineData(null, "gclid=1", "google (anúncio)")]
    [InlineData(null, "utm_source=instagram&utm_medium=paid_social&utm_campaign=ecra", "instagram (anúncio)")]
    [InlineData(null, null, "direto")]
    [InlineData("https://www.pai.pt/lopestech", null, "páginas amarelas")]
    [InlineData("https://lopestech.pt/loja/", null, null)]
    public void Origem(string? referrer, string? query, string? esperado)
        => VisitasSiteService.Origem(referrer, query).Origem.Should().Be(esperado);

    [Fact]
    public void Dispositivo_E_Bots()
    {
        VisitasSiteService.DoUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1")
            .Should().Be(("telemóvel", "iPhone", "Safari"));
        VisitasSiteService.DoUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36")
            .Should().Be(("computador", "Windows", "Chrome"));
        VisitasSiteService.EBot("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)").Should().BeTrue();
        VisitasSiteService.EBot("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)").Should().BeFalse();
        VisitasSiteService.EBot(null).Should().BeTrue();
    }

    private static EventoSite Ev(string sessao, string tipo, int min, string? caminho = null, string? produto = null, string? termo = null,
        int? resultados = null, decimal? valor = null, int? duracao = null, string visitante = "v1", string? origem = null) => new()
    {
        TenantId = Guid.Empty, Visitante = visitante, Sessao = sessao, Tipo = tipo, CreatedAt = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc).AddMinutes(min),
        Caminho = caminho, Produto = produto, Termo = termo, Resultados = resultados, Valor = valor, Duracao = duracao, Origem = origem,
    };

    [Fact]
    public void Agregar_FunilPesquisasEntradasSaidas()
    {
        var eventos = new List<EventoSite>
        {
            Ev("s1", "pagina", 0, "/loja/", origem: "google"),
            Ev("s1", "pesquisa", 1, termo: "capa a57", resultados: 3),
            Ev("s1", "pagina", 2, "/loja/capas/capa-a57"),
            Ev("s1", "produto", 2, produto: "Capa A57"),
            Ev("s1", "carrinho", 3, produto: "Capa A57"),
            Ev("s1", "checkout", 4),
            Ev("s1", "pagamento", 5),
            Ev("s1", "compra", 6, valor: 16.80m),
            Ev("s1", "saida", 7, "/loja/capas/capa-a57", duracao: 90),
            Ev("s2", "pagina", 30, "/", visitante: "v2", origem: "facebook"),
            Ev("s2", "pesquisa", 31, termo: "ecrã a17", resultados: 0, visitante: "v2"),
            Ev("s3", "pagina", 300, "/reparacoes", origem: "direto"),
        };
        var r = VisitasSiteService.Agregar(eventos);
        r.Visitantes.Should().Be(2);
        r.Visitas.Should().Be(3);
        r.Paginas.Should().Be(4);
        r.Funil.Should().Be(new FunilDto(3, 1, 1, 1, 1, 1, 16.80m));
        r.PesquisasSemResultados.Should().ContainSingle().Which.Nome.Should().Be("ecrã a17");
        r.Entradas.Select(e => e.Nome).Should().BeEquivalentTo("/loja/", "/", "/reparacoes");
        r.Origens.Select(o => o.Nome).Should().BeEquivalentTo("google", "facebook", "direto");
        r.VisitasDeUmaPagina.Should().Be(2);
        r.Regressaram.Should().Be(1, "o v1 voltou na s3, 5 horas depois");
        var s1 = VisitasSiteService.Sessoes(eventos).Single(s => s.Sessao == "s1");
        s1.Comprou.Should().BeTrue();
        s1.Saida.Should().Be("/loja/capas/capa-a57");
        s1.DuracaoSeg.Should().Be(420);
    }
}
