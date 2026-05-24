using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Files;

/// <summary>
/// Sprint 246 (Doc 73 Fase A): valida uploads via magic bytes, não apenas Content-Type
/// declarado pelo cliente. Centraliza a tabela de assinaturas + whitelist por categoria.
///
/// **Porquê:** ASP.NET Core <c>IFormFile.ContentType</c> vem do cliente — atacante pode
/// declarar qualquer MIME. Sem inspecção dos primeiros bytes, um <c>evil.exe</c> renomeado
/// para <c>selfie.jpg</c> com <c>Content-Type: image/jpeg</c> passa qualquer whitelist
/// MIME ingenua. Doc 73 §4.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Lê o início do stream, identifica o tipo real e devolve buffer + extensão segura
    /// derivada da detecção (não do FileName). Lança <see cref="ValidationException"/>
    /// se o ficheiro não corresponder ao <paramref name="kind"/> aceite.
    ///
    /// O <paramref name="declaredMime"/> é apenas referência para mensagens — a decisão
    /// é sempre pelos magic bytes.
    /// </summary>
    Task<ValidatedFile> ValidateAsync(Stream content, string declaredMime, FileKind kind, CancellationToken ct = default);
}

public enum FileKind
{
    Image,
    Pdf,
}

public sealed record ValidatedFile(
    string DetectedMime,
    string SafeExtension,
    byte[] Buffer);
