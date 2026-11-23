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

/// <summary>23/11: a qualidade faz parte do artigo — original, OEM e sem placa do mesmo iPhone não se juntam.</summary>
public class QualidadePecaTests
{
    [Theory]
    [InlineData("iPhone 13 Pro - Battery without Board 3095mAh", "sem placa")]
    [InlineData("Bateria para iph 13 Pro (OEM, run diagnostics)", "OEM")]
    [InlineData("Bateria para iph 13 Pro (original)", "original")]
    [InlineData("Touch e lcd com frame Samsung A16 5G azul/preto (Service Pack)", "Service Pack")]
    [InlineData("LCD Assembly With Frame For Samsung Galaxy F13 Refurbished) All Colors)", "recondicionado")]
    [InlineData("Ecrã iPhone 13 Pixdura Soft-OLED Premium", "Soft-OLED")]
    [InlineData("Câmara traseira iPhone 14", null)]
    public void Qualidade_LidaDaDescricao(string descricao, string? esperado)
        => RepairDesk.Services.Artigos.ModeloDispositivo.Qualidade(descricao).Should().Be(esperado);

    [Fact]
    public void BateriasDeQualidadesDiferentes_TemNomesDiferentes()
    {
        var a = RepairDesk.Services.Artigos.ModeloDispositivo.NomePeca("Bateria para iph 13 Pro (original)");
        var b = RepairDesk.Services.Artigos.ModeloDispositivo.NomePeca("iPhone 13 Pro - Battery without Board 3095mAh");
        a.Should().NotBe(b);
        ArtigoService.ChaveNome(a!).Should().NotBe(ArtigoService.ChaveNome(b!));
    }
}
