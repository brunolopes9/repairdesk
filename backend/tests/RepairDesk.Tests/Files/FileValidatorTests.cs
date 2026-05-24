using FluentAssertions;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Files;

namespace RepairDesk.Tests.Files;

/// <summary>
/// Sprint 246 (Doc 73 Fase A): valida que FileValidator detecta correctamente formatos
/// via magic bytes e rejeita ficheiros disfarçados (extensão/MIME não alinhados com conteúdo).
/// </summary>
public class FileValidatorTests
{
    private readonly FileValidator _v = new();

    // === Happy path por formato ===

    [Fact]
    public async Task DetectsJpeg() => await AssertValidImage(JpegBytes(), "image/jpeg", ".jpg");

    [Fact]
    public async Task DetectsPng() => await AssertValidImage(PngBytes(), "image/png", ".png");

    [Fact]
    public async Task DetectsWebp() => await AssertValidImage(WebpBytes(), "image/webp", ".webp");

    [Fact]
    public async Task DetectsGif87a() => await AssertValidImage(Gif87aBytes(), "image/gif", ".gif");

    [Fact]
    public async Task DetectsGif89a() => await AssertValidImage(Gif89aBytes(), "image/gif", ".gif");

    [Fact]
    public async Task DetectsHeic() => await AssertValidImage(HeicBytes(), "image/heic", ".heic");

    [Fact]
    public async Task DetectsPdf()
    {
        var result = await _v.ValidateAsync(new MemoryStream(PdfBytes()), "application/pdf", FileKind.Pdf);
        result.DetectedMime.Should().Be("application/pdf");
        result.SafeExtension.Should().Be(".pdf");
    }

    // === Magic mismatch / disfarçados ===

    [Fact]
    public async Task RejectsExeWithJpegMime()
    {
        // MZ header (Windows executable)
        var fake = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };
        var act = async () => await _v.ValidateAsync(new MemoryStream(fake), "image/jpeg", FileKind.Image);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Code.Should().Be("file_unknown_type");
    }

    [Fact]
    public async Task RejectsPdfPretendingToBeImage()
    {
        // PDF real mas declarado como imagem
        var act = async () => await _v.ValidateAsync(new MemoryStream(PdfBytes()), "image/png", FileKind.Image);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Code.Should().Be("file_type_not_allowed");
    }

    [Fact]
    public async Task RejectsImagePretendingToBePdf()
    {
        var act = async () => await _v.ValidateAsync(new MemoryStream(PngBytes()), "application/pdf", FileKind.Pdf);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Code.Should().Be("file_type_not_allowed");
    }

    [Fact]
    public async Task RejectsSvg()
    {
        // SVG é XML — não tem magic header binário standard que aceitamos
        var svg = System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        var act = async () => await _v.ValidateAsync(new MemoryStream(svg), "image/svg+xml", FileKind.Image);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectsHtmlRenamed()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><script>alert(1)</script></html>");
        var act = async () => await _v.ValidateAsync(new MemoryStream(html), "image/jpeg", FileKind.Image);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectsScriptShellRenamed()
    {
        var sh = System.Text.Encoding.UTF8.GetBytes("#!/bin/bash\nrm -rf /\n");
        var act = async () => await _v.ValidateAsync(new MemoryStream(sh), "image/png", FileKind.Image);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectsEmptyFile()
    {
        var act = async () => await _v.ValidateAsync(new MemoryStream(Array.Empty<byte>()), "image/jpeg", FileKind.Image);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Code.Should().Be("file_empty");
    }

    [Fact]
    public async Task RejectsTruncatedFile()
    {
        // Só 3 bytes — insuficiente para qualquer assinatura
        var act = async () => await _v.ValidateAsync(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "image/jpeg", FileKind.Image);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Code.Should().Be("file_invalid");
    }

    [Fact]
    public async Task BufferIncludesFullContent()
    {
        // Confirma que Buffer devolvido inclui bytes para além do header
        var payload = JpegBytes().Concat(Enumerable.Repeat<byte>(0xAB, 1000)).ToArray();
        var result = await _v.ValidateAsync(new MemoryStream(payload), "image/jpeg", FileKind.Image);
        result.Buffer.Length.Should().Be(payload.Length);
        result.Buffer[^1].Should().Be(0xAB);
    }

    // === Helpers ===

    private async Task AssertValidImage(byte[] bytes, string expectedMime, string expectedExt)
    {
        var result = await _v.ValidateAsync(new MemoryStream(bytes), expectedMime, FileKind.Image);
        result.DetectedMime.Should().Be(expectedMime);
        result.SafeExtension.Should().Be(expectedExt);
        result.Buffer.Should().NotBeEmpty();
    }

    // Headers reais + padding suficiente para passar o check de tamanho mínimo (8 bytes)
    private static byte[] JpegBytes() => Concat(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, FillerBytes(16));
    private static byte[] PngBytes() => Concat(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, FillerBytes(16));
    private static byte[] WebpBytes() => Concat(
        new byte[] { 0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 },
        FillerBytes(16));
    private static byte[] Gif87aBytes() => Concat(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, FillerBytes(16));
    private static byte[] Gif89aBytes() => Concat(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, FillerBytes(16));
    private static byte[] HeicBytes() => Concat(
        new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 },
        FillerBytes(16));
    private static byte[] PdfBytes() => Concat(
        System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n"),
        FillerBytes(16));

    private static byte[] FillerBytes(int n) => Enumerable.Repeat<byte>(0x00, n).ToArray();
    private static byte[] Concat(byte[] a, byte[] b) => a.Concat(b).ToArray();
}
