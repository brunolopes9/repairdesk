using FluentAssertions;
using RepairDesk.Common.Helpers;

namespace RepairDesk.Tests.Common;

public class NifValidatorTests
{
    [Theory]
    // NIFs reais públicos / sintéticos válidos (check-digits calculados manualmente)
    [InlineData("263758141")] // Bruno Lopes (público no roadmap)
    [InlineData("123456789")] // singular sintético
    [InlineData("100000002")] // singular sintético (mínimo)
    [InlineData("200000004")] // singular sintético
    [InlineData("500000000")] // pessoa colectiva sintética
    [InlineData("600000001")] // admin pública
    [InlineData("450000001")] // não-residente (45...)
    public void IsValid_AcceptsValidNifs(string nif)
    {
        NifValidator.IsValid(nif).Should().BeTrue($"'{nif}' devia ser NIF válido");
    }

    [Theory]
    [InlineData("123456788")] // check-digit errado (correcto seria 9)
    [InlineData("000000000")] // primeiro dígito 0 inválido
    [InlineData("400000004")] // primeiro dígito 4 sozinho inválido (só "45..." é válido)
    [InlineData("999999998")] // primeiro 9 OK, check errado (correcto seria 0)
    [InlineData("100000000")] // primeiro 1 OK, check errado (correcto seria 2)
    public void IsValid_RejectsBadCheckDigitOrFirstDigit(string nif)
    {
        NifValidator.IsValid(nif).Should().BeFalse($"'{nif}' tem check-digit ou primeiro dígito inválido");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345678")] // 8 dígitos
    [InlineData("1234567890")] // 10 dígitos
    [InlineData("abc")]
    public void IsValid_RejectsInvalidFormat(string? nif)
    {
        NifValidator.IsValid(nif).Should().BeFalse();
    }

    [Theory]
    [InlineData("123 456 789", "123456789")]
    [InlineData(" 263 758 141 ", "263758141")]
    [InlineData("123-456-789", "123456789")]
    public void Normalize_StripsNonDigits(string input, string expected)
    {
        NifValidator.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void IsValid_HandlesFormattedInputs()
    {
        // NIF válido formatado com espaços ou separadores: deve passar
        NifValidator.IsValid("263 758 141").Should().BeTrue();
        NifValidator.IsValid("263-758-141").Should().BeTrue();
    }
}
