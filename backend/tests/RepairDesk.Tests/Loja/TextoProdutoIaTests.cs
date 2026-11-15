using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

public class TextoProdutoIaTests
{
    [Fact]
    public void Ler_AceitaJsonComTextoAVolta_ELimpaSpecsVazias()
    {
        var r = TextoProdutoIa.Ler("Aqui vai:\n{\"descricao\":\"Ecrã completo.\n\nCom frame.\",\"specs\":[{\"nome\":\"Compatível com\",\"valor\":\"Samsung A52\"},{\"nome\":\"\",\"valor\":\"x\"}],\"seoTitulo\":\"Ecrã Samsung A52\",\"seoDescricao\":\"Envio para todo o país.\"}\nFim");
        r.Should().NotBeNull();
        r!.Descricao.Should().StartWith("Ecrã completo.");
        r.Specs.Should().ContainSingle().Which.Valor.Should().Be("Samsung A52");
        r.SeoTitulo.Should().Be("Ecrã Samsung A52");
    }

    [Theory]
    [InlineData("sem json")]
    [InlineData("{\"specs\":[]}")]
    [InlineData("{partido")]
    public void Ler_RespostaInvalida_DevolveNull(string texto) => TextoProdutoIa.Ler(texto).Should().BeNull();
}
