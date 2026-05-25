using FluentAssertions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Products;

namespace RepairDesk.Tests.Parts;

/// <summary>
/// Sprint 305: cobre o bug confirmado em 2026-05-24 onde ParseGrading agregava B+/C+/AB
/// em "Novo" silenciosamente. CSV Molano real (81 linhas) tinha 46% dos produtos errados.
/// </summary>
public class ProductGradeParseTests
{
    [Theory]
    [InlineData("A", ProductGrading.GradeA, ProductGrade.A)]
    [InlineData("A+", ProductGrading.Premium, ProductGrade.APlus)]
    [InlineData("A++", ProductGrading.Premium, ProductGrade.APlusPlus)]
    [InlineData("AB", ProductGrading.GradeA, ProductGrade.BPlus)]
    [InlineData("B", ProductGrading.GradeB, ProductGrade.B)]
    [InlineData("B+", ProductGrading.GradeB, ProductGrade.BPlus)]
    [InlineData("C", ProductGrading.GradeC, ProductGrade.C)]
    [InlineData("C+", ProductGrading.GradeC, ProductGrade.CPlus)]
    [InlineData("Novo", ProductGrading.Novo, ProductGrade.Sealed)]
    [InlineData("Refurbished", ProductGrading.GradeA, ProductGrade.A)]
    [InlineData("A/B", ProductGrading.GradeA, ProductGrade.BPlus)]
    [InlineData("B/C", ProductGrading.GradeB, ProductGrade.CPlus)]
    [InlineData("A Premium", ProductGrading.Premium, ProductGrade.APlus)]
    // Case-insensitive + whitespace
    [InlineData("  b+  ", ProductGrading.GradeB, ProductGrade.BPlus)]
    [InlineData("c+", ProductGrading.GradeC, ProductGrade.CPlus)]
    public void ParseGradeFromCsv_KnownGrades_MapsCorrectly(
        string input, ProductGrading expectedLegacy, ProductGrade expected2D)
    {
        var result = ProductService.ParseGradeFromCsv(input);

        result.IsValid.Should().BeTrue($"'{input}' deve ser reconhecido");
        result.Legacy.Should().Be(expectedLegacy);
        result.Grade2D.Should().Be(expected2D);
        // Preserva raw exacto (trimmed) — shop usa este field directamente.
        result.Raw.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseGradeFromCsv_BlankOrNull_ReturnsInvalid(string? input)
    {
        var result = ProductService.ParseGradeFromCsv(input);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("branco");
    }

    [Theory]
    [InlineData("Premium Plus")]
    [InlineData("D")]
    [InlineData("xyz")]
    [InlineData("Novos")]
    public void ParseGradeFromCsv_UnknownGrade_ReturnsInvalidWithMessage(string input)
    {
        var result = ProductService.ParseGradeFromCsv(input);

        result.IsValid.Should().BeFalse(
            $"'{input}' não é uma grade conhecida — não pode cair em fallback silencioso");
        result.ErrorMessage.Should().Contain(input);
        result.ErrorMessage.Should().Contain("Aceita:");
    }
}
