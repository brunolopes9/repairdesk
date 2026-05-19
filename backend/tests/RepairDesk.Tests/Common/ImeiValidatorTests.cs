using FluentAssertions;
using RepairDesk.Common.Helpers;

namespace RepairDesk.Tests.Common;

public class ImeiValidatorTests
{
    [Theory]
    [InlineData("490154203237518")]   // Exemplo conhecido válido (3GPP TS 23.003)
    [InlineData("356938035643809")]   // Exemplo Apple iPhone válido
    [InlineData("353918073021384")]   // Outro exemplo válido
    public void IsValid_ReturnsTrue_ForKnownValidImei(string imei)
    {
        ImeiValidator.IsValid(imei).Should().BeTrue();
    }

    [Theory]
    [InlineData("490154203237519")]   // Luhn falha (último dígito errado)
    [InlineData("123456789012345")]   // Inválido aleatório
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345")]              // Curto demais
    [InlineData("ABCDEFGHIJKLMNO")]    // Não-numérico
    public void IsValid_ReturnsFalse_ForInvalidImei(string? imei)
    {
        ImeiValidator.IsValid(imei).Should().BeFalse();
    }

    [Theory]
    [InlineData("490 154 203 237 518")]   // Espaços
    [InlineData("490-154-203-237-518")]   // Hífens
    [InlineData("490.154.203.237.518")]   // Pontos
    public void IsValid_NormalizaSeparadores(string imei)
    {
        ImeiValidator.IsValid(imei).Should().BeTrue();
    }

    [Fact]
    public void Normalize_RemoveSeparadoresEMantemSoDigitos()
    {
        ImeiValidator.Normalize("490 154-203.237/518").Should().Be("490154203237518");
        ImeiValidator.Normalize(null).Should().Be("");
    }

    [Fact]
    public void Mask_PreservaTacInicialEUltimos4_MascaraOMeio()
    {
        var masked = ImeiValidator.Mask("490154203237518");
        masked.Should().StartWith("490");
        masked.Should().EndWith("7518");
        masked.Should().HaveLength(15);
        masked.Should().NotContain("154203");
    }

    [Fact]
    public void Mask_ImeiCurto_DevolvePlaceholder()
    {
        ImeiValidator.Mask("123").Should().Be("***");
        ImeiValidator.Mask(null).Should().Be("***");
    }
}
