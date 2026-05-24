using FluentAssertions;
using RepairDesk.Common.Helpers;

namespace RepairDesk.Tests.Files;

/// <summary>
/// Sprint 248 (Doc 73 Fase C): garantias do sanitizer de nomes — defesa contra path
/// traversal, nomes reservados Windows, caracteres especiais e limites de tamanho.
/// </summary>
public class FileNameSanitizerTests
{
    [Theory]
    [InlineData(null, "file")]
    [InlineData("", "file")]
    [InlineData("   ", "file")]
    [InlineData("foto.jpg", "foto.jpg")]
    [InlineData("foto 1.jpg", "foto 1.jpg")]
    [InlineData("a-b_c.png", "a-b_c.png")]
    public void Trivial(string? input, string expected)
        => FileNameSanitizer.Safe(input).Should().Be(expected);

    [Fact]
    public void StripsPathTraversalUnix()
        => FileNameSanitizer.Safe("../../etc/passwd").Should().Be("passwd");

    [Fact]
    public void StripsPathTraversalWindows()
        => FileNameSanitizer.Safe("..\\..\\Windows\\System32\\cmd.exe").Should().Be("cmd.exe");

    [Fact]
    public void StripsLeadingDotsAndSpaces()
        => FileNameSanitizer.Safe("  ...evil.jpg  ").Should().Be("evil.jpg");

    [Fact]
    public void EscapesWindowsReservedCon()
        => FileNameSanitizer.Safe("con.txt").Should().StartWith("_");

    [Fact]
    public void EscapesWindowsReservedComN()
        => FileNameSanitizer.Safe("com1.log").Should().StartWith("_");

    [Fact]
    public void EscapesWindowsReservedLptN()
        => FileNameSanitizer.Safe("lpt9.cfg").Should().StartWith("_");

    [Fact]
    public void DoesNotEscapeComWithoutDigit()
        => FileNameSanitizer.Safe("commission.pdf").Should().Be("commission.pdf");

    [Fact]
    public void RemovesQuotesAndAngleBrackets()
        => FileNameSanitizer.Safe("<script>x.jpg").Should().Be("scriptx.jpg");

    [Fact]
    public void RemovesNonAscii()
        => FileNameSanitizer.Safe("fötö.jpg").Length.Should().BeGreaterThan(0);

    [Fact]
    public void TruncatesAtMaxLength()
    {
        var raw = new string('a', 300) + ".jpg";
        var safe = FileNameSanitizer.Safe(raw);
        safe.Length.Should().Be(100);
    }

    [Fact]
    public void NeverReturnsEmpty()
        => FileNameSanitizer.Safe("???").Should().NotBeNullOrEmpty();

    [Fact]
    public void CustomMaxLength()
        => FileNameSanitizer.Safe("aaaaaaaaaaaaaaaaa.jpg", maxLength: 10).Length.Should().Be(10);
}
