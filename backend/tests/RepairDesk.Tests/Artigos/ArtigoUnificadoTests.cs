using FluentAssertions;
using RepairDesk.Services.Artigos;

namespace RepairDesk.Tests.Artigos;

/// <summary>23/11 (Bruno): toda a compra é artigo — mesmos nomes juntam-se e as peças vão para «Peças».</summary>
public class ArtigoUnificadoTests
{
    [Theory]
    [InlineData("Bateria iPhone 5s", "BATERIA IPHONE 5S")]
    [InlineData("Batería A1773", "Bateria  a1773")]
    [InlineData("Tampa traseira iPhone 14 Pro roxo", "Tampa traseira iPhone 14 pro roxo")]
    public void ChaveNome_IgnoraAcentosMaiusculasEEspacos(string a, string b)
        => ArtigoService.ChaveNome(a).Should().Be(ArtigoService.ChaveNome(b));

    [Theory]
    [InlineData("Flex de carga iph 8 preto", "Peças")]
    [InlineData("IC de carga MAX77705C Samsung Note 10+", "Peças")]
    [InlineData("Batería A1773 para airpod 1 y 2 50MAH", "Peças")]
    [InlineData("Câmara iPhone 14", "Peças")]
    [InlineData("Protetor de câmara metálico iPhone 15", "Acessórios")]
    [InlineData("Capa TPU iPhone 13 Pro verde", "Acessórios")]
    [InlineData("Cartão microSDXC 64 GB Intenso", "Acessórios")]
    [InlineData("Cola B7000 110 ml", "Acessórios")]
    public void Categoria_PeloNome(string nome, string esperado)
        => ArtigoService.CategoriaPeloNome(nome).Should().Be(esperado);
}
