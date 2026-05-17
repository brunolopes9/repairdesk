namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Abstracção de armazenamento de binários (fotos).
/// Implementações: LocalFileSystem (volume Docker), Cloudflare R2 (S3-compat).
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Faz upload do stream para a chave dada (sobrescreve se existir).</summary>
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Devolve stream de leitura. Caller é responsável por fechar.</summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <summary>Apaga binário. Idempotente (não falha se não existir).</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>True se chave existir.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
